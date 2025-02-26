using ImmichTools.Json;
using ImmichTools.ReplyData;
using ImmichTools.RequestData;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Web;

namespace ImmichTools.Tools;

// TODO Use Polly for resilience

internal class AutoStack
{

	private static readonly SemaphoreSlim _semaphore = new(5); // Adjust concurrency limit

	private static async Task<T?> GetWithThrottle<T>(HttpClient client, string url, JsonTypeInfo<T> context)
	{
		await _semaphore.WaitAsync();
		try
		{
			Console.Write(".");
			return await GetWithRetry<T>(client, url, context);
		}
		finally
		{
			_semaphore.Release();
		}
	}

	private static HttpClient CreateHttpClient(string host, string apiKey)
	{
		var client = new HttpClient();
		client.BaseAddress = new Uri(host);
		client.DefaultRequestHeaders.Add("x-api-key", apiKey);
		return client;
	}

	internal static async Task RunAsync(string host, string apiKey, string directory, bool recursive, bool copyMetadata, bool dryRun)
	{
		var client = CreateHttpClient(host, apiKey);
		var directories = recursive
				? await GetDirectoriesRecursiveAsync(directory, client)
				: [directory];

		// var assetTasks = directories.Select(d => client.GetFromJsonAsync<Asset[]>(
		// 		"/api/view/folder?path=" + HttpUtility.UrlEncode(d),
		// 		SerializerContext.Default.AssetArray));
		var assetTasks = directories.Select(d => GetWithThrottle<Asset[]>(
				client, "/api/view/folder?path=" + HttpUtility.UrlEncode(d), SerializerContext.Default.AssetArray));
		var assetArrays = await Task.WhenAll(assetTasks);
		var assets = assetArrays.SelectMany(a => a ?? []).ToArray();

		Console.WriteLine("");
		if (assets == null || assets.Length == 0)
		{
			Console.WriteLine("❌ No assets found in {0}", directory);
			return;
		}

		// var assetDetailTasks = assets.Select(a => client.GetFromJsonAsync<AssetDetail>(
		// 		"/api/assets/" + HttpUtility.UrlEncode(a.Id),
		// 		SerializerContext.Default.AssetDetail));
		var assetDetailTasks = assets.Select(a => GetWithThrottle<AssetDetail>(
				client, "/api/assets/" + HttpUtility.UrlEncode(a.Id), SerializerContext.Default.AssetDetail));
		var assetDetailArrays = await Task.WhenAll(assetDetailTasks);
		var assetDetails = assetDetailArrays.Where(a => a != null && a.Stack == null).Select(a => a!).ToArray();

		Console.WriteLine("");
		if (assetDetails == null || assetDetails.Length == 0)
		{
			Console.WriteLine("❌ No un-stacked assets found in {0}", directory);
			return;
		}

		if (dryRun)
		{
			Console.WriteLine("⭐⭐⭐ DRY RUN CHANGES ⭐⭐⭐");
		}

		var groupedAssets = assetDetails.GroupBy<Asset, string>(GetBaseName).Where(g => g.Count() > 1).ToArray();
		var stackCount = groupedAssets.Length;

		if (stackCount == 0)
		{
			Console.WriteLine("❌ No stackable assets found in {0}", directory);
			return;
		}

		var i = 1;
		foreach (var group in groupedAssets)
		{
			var sortedAssets = group.OrderByDescending(GetFileTypePriority)
					.ThenByDescending(a => Path.GetDirectoryName(Path.GetRelativePath(directory, a.OriginalPath)))
					.ThenBy(a => Path.GetFileNameWithoutExtension(a.OriginalFileName))
					.ToArray();

			Console.WriteLine("Stack {0}/{1}: {2}", i, stackCount, string.Join(", ", sortedAssets.Select(a => a.OriginalFileName).ToArray()));

			if (!dryRun)
			{
				await PostWithRetry(client,
						"/api/stacks",
						new CreateStack { AssetIds = sortedAssets.Select(a => a.Id).ToList() },
						SerializerContext.Default.CreateStack);
			}

			// TODO - Add metadata for _a, _b, etc. files

			if (copyMetadata)
			{
				var rawImageAsset = sortedAssets.LastOrDefault(a => GetFileTypePriority(a) == 0);
				if (rawImageAsset != null)
				{
					foreach (var asset in sortedAssets.Where(a => a.LocalDateTime != rawImageAsset.LocalDateTime))
					{
						Console.WriteLine("Copying metadata from {0} to {1}", rawImageAsset.OriginalFileName, asset.LocalDateTime);
						if (!dryRun)
						{
							await client.PutAsJsonAsync(
									$"/api/assets/{asset.Id}",
									new UpdateAsset
									{
										DateTimeOriginal = rawImageAsset.LocalDateTime,
										Latitude = rawImageAsset.ExifInfo.Latitude,
										Longitude = rawImageAsset.ExifInfo.Longitude
									},
									SerializerContext.Default.UpdateAsset);
						}
					}
				}
			}
			i++;
		}
	}

	private static async Task<IEnumerable<string>> GetDirectoriesRecursiveAsync(string directory, HttpClient client)
	{
		IEnumerable<string> directories = await GetWithRetry(client, "/api/view/folder/unique-paths", SerializerContext.Default.StringArray) ?? [directory];
		if (directory.StartsWith("/"))
		{
			directories = directories.Select(d => d.StartsWith("/") ? d : "/" + d);
		}
		return directories.Where(d => !Path.GetRelativePath(directory, d).StartsWith(".."));
	}

	private static readonly HashSet<string> RawExtensions = [".cr2", ".cr3", ".dng"];

	private static int GetFileTypePriority(Asset asset)
	{
		// TODO Maybe add logic to verify xxx.ext > xxx_a.ext > xxx_b.ext order

		var extension = Path.GetExtension(asset.OriginalFileName).ToLowerInvariant();
		return RawExtensions.Contains(extension) ? 0 : 1;
	}

	private static readonly Regex BaseNameRegex = new Regex("\\A(?<BaseName>[a-zA-Z_]+_[0-9]+)([_-].*)?\\Z");

	private static string GetBaseName(Asset asset)
	{
		var withoutExtension = Path.GetFileNameWithoutExtension(asset.OriginalFileName);
		var match = BaseNameRegex.Match(withoutExtension);
		return match.Success ? match.Groups["BaseName"].Value : withoutExtension;
	}

	private static async Task<T?> GetWithRetry<T>(HttpClient client, string url, JsonTypeInfo<T> context, int maxRetries = 3)
	{
		int attempt = 0;
		while (true)
		{
			try
			{
				return await client.GetFromJsonAsync<T>(url, context);
			}
			catch (HttpRequestException ex) when (attempt < maxRetries)
			{
				int delay = (int)Math.Pow(2, attempt) * 100; // 100ms, 200ms, 400ms...
				Console.WriteLine($"Request failed: {ex.Message}. Retrying in {delay}ms...");
				await Task.Delay(delay);
				attempt++;
			}
		}
	}
	private static async Task<HttpResponseMessage> PostWithRetry<T>(HttpClient client, string url, T payload, JsonTypeInfo<T> context, int maxRetries = 3)
	{
		int attempt = 0;
		while (true)
		{
			try
			{
				return await client.PostAsJsonAsync(url, payload, context);
			}
			catch (HttpRequestException ex) when (attempt < maxRetries)
			{
				int delay = (int)Math.Pow(2, attempt) * 100;
				Console.WriteLine($"POST failed: {ex.Message}. Retrying in {delay}ms...");
				await Task.Delay(delay);
				attempt++;
			}
		}
	}
}
