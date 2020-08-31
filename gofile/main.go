package main

import (
	"flag"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"strings"
)

var (
	dir    = "./"
	listen = "127.0.0.1:8021"
)

func handle(w http.ResponseWriter, r *http.Request) {
	if r.Method == "GET" {
		download(w, r)
	} else if r.Method == "POST" {
		upload(w, r)
	} else {
		response(w, 405)
	}
}

func validateFilename(filename string) bool {
	return len(filename) <= 255 && !strings.HasPrefix(filename, "/") && !strings.Contains(filename, "..")
}

func response(w http.ResponseWriter, code int) {
	w.WriteHeader(code)
	w.Write(nil)
}

func download(w http.ResponseWriter, r *http.Request) {
	var filename = strings.TrimLeft(r.URL.Path, "/")
	if !validateFilename(filename) {
		response(w, 403)
		return
	}
	filename = filepath.Join(dir, filename)
	info, err := os.Stat(filename)
	if err != nil || info.IsDir() {
		response(w, 404)
		return
	}
	file, err := os.Open(filename)
	if err != nil {
		response(w, 500)
		return
	}
	defer file.Close()
	// file stream response
	w.Header().Set("Content-Type", "application/octet-stream")
	_, err = io.Copy(w, file)
	if err != nil {
		fmt.Printf("failed to write response: %v\n", err)
		return
	}
	fmt.Printf("download file %s\n", filename)
}

func upload(w http.ResponseWriter, r *http.Request) {
	reader, err := r.MultipartReader()
	if err != nil {
		response(w, 400)
		return
	}
	for {
		part, err := reader.NextPart()
		if err != nil {
			if err == io.EOF {
				break
			} else {
				response(w, 400)
				return
			}
		}
		defer part.Close()

		var filename = part.FileName()
		if filename == "" {
			response(w, 400)
			return
		}
		if !validateFilename(filename) {
			response(w, 403)
			return
		}

		if err = saveFile(filename, part); err != nil {
			fmt.Printf("failed to save file: %v\n", err)
			response(w, 500)
			return
		}
		fmt.Printf("upload file %s\n", filename)
	}
	w.Write([]byte("success\n"))
}

func saveFile(filename string, reader io.Reader) error {
	filename = filepath.Join(dir, filename)
	file, err := os.Create(filename)
	if err != nil {
		return err
	}
	defer file.Close()
	_, err = io.Copy(file, reader)
	if err != nil {
		return err
	}
	return nil
}

func main() {
	flag.StringVar(&dir, "d", dir, "root directory")
	flag.StringVar(&listen, "l", listen, "listen address")
	flag.Parse()

	fmt.Println(http.ListenAndServe(listen, http.HandlerFunc(handle)))
}
