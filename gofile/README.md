# gofile

Start a http server for file downloading and uploading.

```
Usage of gofile:
  -d string
        root directory (default "./")
  -l string
        listen address (default "127.0.0.1:8021")
  -k string
        authentication key. Optioanl.
```

### download

```shell
curl http://127.0.0.1:8021/README.md -H 'X-AUTH-KEY: xxxx' -o README.md
```

### upload

```shell
curl http://127.0.0.1:8021 -H 'X-AUTH-KEY: xxxx' -X POST -F 'file=@xxx'
```
