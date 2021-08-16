package main

import (
	"flag"
	"fmt"
	"os"
	"os/exec"
	"runtime"

	qrcode "github.com/skip2/go-qrcode"
	qrcode2 "github.com/tuotoo/qrcode"
)

func main() {
	var content, filename string
	var size int
	flag.StringVar(&content, "c", "", "content to create qrcode")
	flag.StringVar(&filename, "f", "", "input/output file name")
	flag.IntVar(&size, "s", 256, "image size")
	flag.Parse()
	if content != "" {
		if filename == "" {
			filename = "qrcode.png"
		}
		if err := qrcode.WriteFile(content, qrcode.Medium, size, filename); err != nil {
			fmt.Println(err)
			return
		}
		if runtime.GOOS == "darwin" {
			cmd := exec.Command("open", filename)
			cmd.Start()
		} else if runtime.GOOS == "windows" {
			cmd := exec.Command("explorer", filename)
			cmd.Start()
		}
	} else if filename != "" {
		file, err := os.Open(filename)
		if err != nil {
			fmt.Println(err)
			return
		}
		matrix, err := qrcode2.Decode(file)
		if err != nil {
			fmt.Println(err)
			return
		}
		fmt.Println(matrix.Content)
	} else {
		flag.Usage()
	}
}
