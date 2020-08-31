package main

import (
	"io/ioutil"
	"net"

	"fmt"
)

func main() {
	// receive udp packge and write remote ip to file when udp package content is string "justlovediaodiao"
	addr, err := net.ResolveUDPAddr("udp", ":1333")
	conn, err := net.ListenUDP("udp", addr)
	if err != nil {
		fmt.Println(err)
		return
	}
	defer conn.Close()
	var buff = make([]byte, 16)
	for {
		n, addr, err := conn.ReadFromUDP(buff)
		if err != nil {
			continue
		}
		if n == 16 && string(buff) == "justlovediaodiao" {
			ioutil.WriteFile("/root/router.ip", []byte(addr.IP.String()), 0644)
		}
	}
}
