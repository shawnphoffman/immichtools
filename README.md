# immichtools

Collection of command line tools to interact with Immich via its API forked from https://github.com/dfyx/immichtools

## Usage

**PC:**

```
ImmichTools.exe autostack -h "https://immich.example.com/" -k mysupersecretapikey -r -m -d "/directory"
```

**Mac:**

```
ImmichTools autostack -h "http://localhost:2283/" -k mysupersecretapikey "/directory"
```

## Reference

    SYNOPSIS
        ImmichTools.exe command [options] [parameters]

    COMMANDS:
        autostack <directory_to_search>
            Automatically combines assets with matching basename into stacks

    OPTIONS:
        -h  --host      The Immich host to talk to
        -k  --api-key   The Immich API key
        -r              Recursively include subdirectories
        -c              Copy metadata from raw image to edited versions
        -d              Print what would be done without actually doing it

## Notes

You can use environment variables instead of command line arguments if you copy `.env.example` to `.env` and update the values.
