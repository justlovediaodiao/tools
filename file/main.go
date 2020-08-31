package main

import (
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"path"
	"strconv"
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

func serve(dir string, port int) {
	directory = dir
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
	// usage: gofile [<dir>] [<port>]
	dir, port, err := parseArgs()
	if err != nil {
		return
	}
	serve(dir, port)
}
