package main

import (
	"flag"
	"fmt"
	"net"
	"net/http"
	"os"
	"strings"

	"github.com/justlovediaodiao/tools/file/down"
	"github.com/justlovediaodiao/tools/file/text"
	"github.com/justlovediaodiao/tools/file/up"
)

func serve(addr string, handler http.Handler) {
	fmt.Printf("http://%s\n", addr)
	if err := http.ListenAndServe(addr, handler); err != nil {
		if opErr, ok := err.(*net.OpError); ok && opErr.Op == "listen" {
			fmt.Printf("can not listen on %s\n", addr)
		}
	}
}

func lanIP() string {
	addrs, err := net.InterfaceAddrs()
	if err != nil {
		return "0.0.0.0"
	}
	for _, address := range addrs {
		if ipnet, ok := address.(*net.IPNet); ok && ipnet.IP.IsPrivate() {
			return ipnet.IP.String()
		}
	}
	return "0.0.0.0"
}

type FlagSets map[string]*flag.FlagSet

func (f FlagSets) Add(cmd string) *flag.FlagSet {
	fs := flag.NewFlagSet(cmd, flag.ExitOnError)
	f[cmd] = fs
	return fs
}

func (f FlagSets) Parse() string {
	var cmd string
	if len(os.Args) > 1 {
		cmd = os.Args[1]
	}
	fs, ok := f[cmd]
	if !ok {
		f.Usage()
		os.Exit(0)
	}
	fs.Parse(os.Args[2:])
	return cmd
}

func (f FlagSets) Usage() {
	fmt.Printf("Usage of %s:\n", os.Args[0])
	for _, fs := range f {
		fmt.Printf("%s\n", fs.Name())
		fs.PrintDefaults()
	}
}

func main() {
	var listen, dir string

	root := make(FlagSets)
	fs := root.Add("down")
	fs.StringVar(&listen, "l", "80", "listen address or port")
	fs.StringVar(&dir, "d", "./", "serve directory")

	fs = root.Add("up")
	fs.StringVar(&listen, "l", "80", "listen address or port")
	fs.StringVar(&dir, "d", "./", "serve directory")

	fs = root.Add("text")
	fs.StringVar(&listen, "l", "80", "listen address or port")

	cmd := root.Parse()

	f := func(addr string) string {
		if strings.Contains(addr, ":") {
			return addr
		}
		return fmt.Sprintf("%s:%s", lanIP(), addr)
	}

	switch cmd {
	case "down":
		serve(f(listen), down.Handler(dir))
	case "up":
		serve(f(listen), up.Handler(dir))
	case "text":
		serve(f(listen), text.Handler())
	default:
		root.Usage()
	}
}
