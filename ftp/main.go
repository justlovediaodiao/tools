package main

import (
	"errors"
	"fmt"
	"net"
	"net/http"
	"os"
	"strconv"
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
	return ""
}

func wrap(f http.Handler) http.Handler {
	var h = func(w http.ResponseWriter, r *http.Request) {
		w.Header()["Content-Type"] = []string{"application/octet-stream"}
		f.ServeHTTP(w, r)
	}
	return http.HandlerFunc(h)
}

func serve(dir string, port int) {
	var ip = lanIP()
	if ip == "" {
		ip = "0.0.0.0"
	}
	var addr = fmt.Sprintf("%s:%d", ip, port)
	fmt.Printf("http://%s\n", addr)
	var h = http.FileServer(http.Dir(dir))
	var err = http.ListenAndServe(addr, wrap(h))
	if e, ok := err.(*net.OpError); ok && e.Op == "listen" {
		fmt.Printf("can not listen on %s.\n", addr)
	}
}

func parseArgs() (dir string, port int, err error) {
	if len(os.Args) == 3 {
		dir = os.Args[1]
		port, err = strconv.Atoi(os.Args[2])
		if err != nil {
			fmt.Println(err)
			return
		}
	} else if len(os.Args) == 2 {
		dir = os.Args[1]
	}
	if dir != "" {
		_, err = os.Stat(dir)
		if err != nil {
			fmt.Println(err)
			return
		}
	} else {
		dir, err = os.Getwd()
		if err != nil {
			fmt.Println(err)
			return
		}
	}
	if port != 0 {
		if port < 1 || port > 65535 {
			fmt.Println("port out of range")
			err = errors.New("port out of range")
			return
		}
	} else {
		port = 80
	}
	return
}

func main() {
	// usage: goftp [<dir>] [<port>]
	dir, port, err := parseArgs()
	if err != nil {
		return
	}
	serve(dir, port)
}
