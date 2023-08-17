package main

import (
	"flag"
	"fmt"
	"net"
	"strings"

	"github.com/justlovediaodiao/tools/sync/file"
	"github.com/justlovediaodiao/tools/sync/ftp"
	"github.com/justlovediaodiao/tools/sync/text"
)

func lanIP() string {
	addrs, err := net.InterfaceAddrs()
	if err != nil {
		return ""
	}
	for _, address := range addrs {
		if ipnet, ok := address.(*net.IPNet); ok && ipnet.IP.To4() != nil {
			// C
			if ipnet.IP[12] == 192 && ipnet.IP[13] == 168 {
				return ipnet.IP.String()
			}
			// A
			if ipnet.IP[12] == 10 {
				return ipnet.IP.String()
			}
			// B
			if ipnet.IP[12] == 172 && ipnet.IP[13] >= 16 && ipnet.IP[13] <= 31 {
				return ipnet.IP.String()
			}
		}
	}
	return "0.0.0.0"
}

func main() {
	var service, listen, dir string
	flag.StringVar(&service, "s", "", "service name, ftp or file or text")
	flag.StringVar(&listen, "l", "80", "listen address or port")
	flag.StringVar(&dir, "d", "./", "serve directory")
	flag.Parse()
	if !strings.Contains(listen, ":") {
		listen = fmt.Sprintf("%s:%s", lanIP(), listen)
	}
	switch service {
	case "ftp":
		ftp.Serve(listen, dir)

	case "file":
		file.Serve(listen, dir)
	case "text":
		text.Serve(listen)
	default:
		flag.Usage()
	}
}
