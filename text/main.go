package main

import (
	"flag"
	"fmt"
	"net"
	"net/http"
	"strings"
)

var text = ""

func handle(w http.ResponseWriter, r *http.Request) {
	var res string
	if r.Method == "GET" {
		res = render(text)
	} else if r.Method == "POST" {
		err := r.ParseForm()
		if err != nil {
			fmt.Println(err)
			res = "Bad Request"
		}
		t, ok := r.PostForm["text"]
		if ok {
			text = t[0]
		}
		res = render(text)
	} else {
		w.WriteHeader(405)
		res = "405 Method Not Allowed"
	}
	w.Write([]byte(res))
}

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

func serve(listen string) {
	fmt.Printf("http://%s\n", listen)
	http.HandleFunc("/", handle)
	var err = http.ListenAndServe(listen, nil)
	if e, ok := err.(*net.OpError); ok && e.Op == "listen" {
		fmt.Printf("can not listen on %s\n", listen)
	}
}

func main() {
	var listen string
	flag.StringVar(&listen, "l", "80", "listen address or port")
	flag.Parse()
	if !strings.Contains(listen, ":") {
		listen = fmt.Sprintf("%s:%s", lanIP(), listen)
	}
	serve(listen)
}
