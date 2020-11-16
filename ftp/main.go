package main

import (
	"flag"
	"fmt"
	"net"
	"net/http"
	"strings"
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

func serve(listen string, dir string) {
	fmt.Printf("http://%s\n", listen)
	var h = http.FileServer(http.Dir(dir))
	var err = http.ListenAndServe(listen, wrap(h))
	if e, ok := err.(*net.OpError); ok && e.Op == "listen" {
		fmt.Printf("can not listen on %s.\n", listen)
	}
}

func main() {
	var listen, dir string
	flag.StringVar(&listen, "l", "80", "listen address or port")
	flag.StringVar(&dir, "d", "./", "serve directory")
	flag.Parse()
	if !strings.Contains(listen, ":") {
		listen = fmt.Sprintf("%s:%s", lanIP(), listen)
	}
	serve(listen, dir)
}
