package main

import (
	"errors"
	"fmt"
	"net"
	"net/http"
	"os"
	"strconv"
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

func serve(port int) {
	var ip = lanIP()
	if ip == "" {
		ip = "0.0.0.0"
	}
	var addr = fmt.Sprintf("%s:%d", ip, port)
	fmt.Printf("http://%s\n", addr)
	http.HandleFunc("/", handle)
	var err = http.ListenAndServe(addr, nil)
	if e, ok := err.(*net.OpError); ok && e.Op == "listen" {
		fmt.Printf("can not listen on %s\n", addr)
	}
}

func parseArgs() (port int, err error) {
	if len(os.Args) == 2 {
		port, err = strconv.Atoi(os.Args[1])
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
	// usage: gotext [<port>]
	port, err := parseArgs()
	if err != nil {
		return
	}
	serve(port)
}
