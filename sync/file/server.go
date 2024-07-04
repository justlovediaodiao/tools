package file

import (
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"path"
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

	read := 0
	total := int(r.ContentLength)
	last := 0
	callback := func(n int) {
		if total > 0 {
			read += n
			nn := read
			if n == 0 { // 0 means end
				nn = total
			}
			if t := nn * 100 / total; t > last {
				progressBar(total, nn)
				last = t
			}
		}
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
			var res = saveFile(filename, part, callback)
			if res != "" {
				return res
			}
		}
	}
	return "Success"
}

func saveFile(filename string, reader io.Reader, callback func(int)) string {
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
	err = ioCopy(file, reader, callback)
	if err != nil {
		return "Error"
	}
	return ""
}

func ioCopy(dst io.Writer, src io.Reader, callback func(int)) error {
	size := 512 * 1024
	buf := make([]byte, size)
	for {
		n, err := src.Read(buf)
		if n > 0 {
			_, err = dst.Write(buf[:n])
			if err != nil {
				return err
			}
			callback(n)
		}
		if err != nil {
			if err == io.EOF {
				callback(0)
				return nil
			}
			return err
		}
	}
}

func progressBar(total, progress int) {
	barLength := 50
	filledLength := barLength * progress / total
	bar := make([]byte, barLength, barLength)
	for i := 0; i < barLength; i++ {
		if i < filledLength {
			bar[i] = '#'
		} else {
			bar[i] = '-'
		}
	}
	percent := progress * 100 / total
	fmt.Printf("\r[%s]%d%%", string(bar), percent)
	if percent == 100 {
		fmt.Println()
	}
}

func Serve(listen string, dir string) {
	fmt.Printf("http://%s\n", listen)
	http.HandleFunc("/", handle)
	var err = http.ListenAndServe(listen, nil)
	if e, ok := err.(*net.OpError); ok && e.Op == "listen" {
		fmt.Printf("can not listen on %s\n", listen)
	}
}
