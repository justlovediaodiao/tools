# tunnel

tunnel is a tcp/udp tunnel, it relays tcp/udp data.

### usage

```shell
tunnel -l :443 -t github.com:443
```

- l: local listen address
- t: target address

It will listen on 443 port for tcp and udp, relay to github.com:443.
