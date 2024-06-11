package main

import (
	"encoding/hex"
	"flag"
	"fmt"
	"net"
	"strings"
)

func buildPayload(mac string) ([]byte, error) {
	mac = strings.ReplaceAll(mac, ":", "")
	mac = strings.ReplaceAll(mac, "-", "")
	pack := "FFFFFFFFFFFF" + strings.Repeat(mac, 16)
	payload, err := hex.DecodeString(pack)
	if err != nil {
		return nil, err
	}
	return payload, nil
}

func sendPayload(addr string, payload []byte) error {
	conn, err := net.Dial("udp", addr)
	if err != nil {
		return err
	}
	_, err = conn.Write(payload)
	return err
}

func main() {
	var mac string
	var addr string
	flag.StringVar(&mac, "mac", "", "MAC address")
	flag.StringVar(&addr, "addr", "", "ip address")
	flag.Parse()
	if mac == "" || addr == "" {
		flag.Usage()
		return
	}
	payload, err := buildPayload(mac)
	if err != nil {
		fmt.Println(err)
		return
	}
	if err = sendPayload(addr, payload); err != nil {
		fmt.Println(err)
		return
	}
	fmt.Println("success")
}
