package main

import (
	"flag"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"path"
	"strings"
)

var directory string

func handle(w http.ResponseWriter, r *http.Request) {
	var res string
	if r.Method == "GET" {
		res = render("")
	} else if r.Method == "POST" {
		res = post(r)
		res = render(res)
	} else {
		w.WriteHeader(405)
		res = "405 Method Not Allowed"
	}
	w.Write([]byte(res))
}

func post(r *http.Request) string {
	reader, err := r.MultipartReader()
	if err != nil {
		return "Bad Request"
	}
	for {
		part, err := reader.NextPart()
		if err != nil {
			if err == io.EOF {
				break
			} else {
				return "Bad Request"
			}
		}
		var filename = part.FileName()
		if filename != "" {
			defer part.Close()
			var res = saveFile(filename, part)
			if res != "" {
				return res
			}
		}
	}
	return "Success"
}

func saveFile(filename string, reader io.Reader) string {
	filename = path.Join(directory, filename)
	_, err := os.Stat(filename)
	if err == nil {
		return "File Exists"
	}
	file, err := os.Create(filename)
	if err != nil {
		return "Error"
	}
	defer file.Close()
	_, err = io.Copy(file, reader)
	if err != nil {
		return "Error"
	}
	return ""
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

func serve(listen string, dir string) {
	fmt.Printf("http://%s\n", listen)
	http.HandleFunc("/", handle)
	var err = http.ListenAndServe(listen, nil)
	if e, ok := err.(*net.OpError); ok && e.Op == "listen" {
		fmt.Printf("can not listen on %s\n", listen)
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
