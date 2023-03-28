#!/usr/bin/env python3

import socket

def capture():
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(('127.0.0.1', 53))
    b, addr = sock.recvfrom(65535)
    with open('dnsq', 'wb') as fp:
        fp.write(b)

    nat = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    nat.bind(('0.0.0.0', 0))
    nat.sendto(b, ('1.0.0.1', 53))
    b, _ = nat.recvfrom(65535)
    with open('dnsr', 'wb') as fp:
        fp.write(b)

    sock.sendto(b, addr)

    sock.close()
    nat.close()

if __name__ == '__main__':
    capture()
    # run in shell: nslookup github.com 127.0.0.1
