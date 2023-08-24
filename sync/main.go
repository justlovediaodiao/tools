package main

import (
	"flag"
	"fmt"
	"net"
	"os"
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
	r := "0.0.0.0"
	for _, address := range addrs {
		if ipnet, ok := address.(*net.IPNet); ok && ipnet.IP.To4() != nil {
			if ipnet.IP[12] == 10 { // A
				ip := ipnet.IP.String()
				r = ip
			} else if ipnet.IP[12] == 172 && ipnet.IP[13] >= 16 && ipnet.IP[13] <= 31 { // B
				ip := ipnet.IP.String()
				r = ip
			} else if ipnet.IP[12] == 192 && ipnet.IP[13] == 168 { // C
				ip := ipnet.IP.String()
				return ip // use 192.168.x.x first
			}
		}
	}
	return r
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
	fs := root.Add("ftp")
	fs.StringVar(&listen, "l", "80", "listen address or port")
	fs.StringVar(&dir, "d", "./", "serve directory")

	fs = root.Add("file")
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
	case "ftp":
		ftp.Serve(f(listen), dir)
	case "file":
		file.Serve(f(listen), dir)
	case "text":
		text.Serve(f(listen))
	default:
		root.Usage()
	}
}
