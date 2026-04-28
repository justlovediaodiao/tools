package up

import (
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
)

func handler(dir string) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		var res string
		if r.Method == "GET" {
			res = render("")
		} else if r.Method == "POST" {
			res = post(r, dir)
			res = render(res)
		} else {
			w.WriteHeader(405)
			res = "405 Method Not Allowed"
		}
		w.Write([]byte(res))
	}
}

type pReader struct {
	reader io.Reader
	total  int
	read   int
	last   int
}

func (p *pReader) Read(buf []byte) (int, error) {
	n, err := p.reader.Read(buf)
	if n > 0 && p.total > 0 {
		p.read += n
		t := p.read * 100 / p.total
		if t > p.last {
			progressBar(p.total, p.read)
			p.last = t
		}
	}
	return n, err
}

func (p *pReader) Close() error {
	fmt.Println()
	return nil
}

func post(r *http.Request, dir string) string {
	reader, err := r.MultipartReader()
	if err != nil {
		return "Bad Request"
	}
	rd := pReader{total: int(r.ContentLength)}
	defer rd.Close()
	for {
		part, err := reader.NextPart()
		if err != nil {
			if err == io.EOF {
				break
			}
			return "Bad Request"
		}
		filename := part.FileName()
		if filename != "" {
			rd.reader = part
			res := saveFile(dir, filename, &rd)
			part.Close()
			if res != "" {
				return res
			}
		} else {
			part.Close()
		}
	}
	return "Success"
}

func saveFile(dir, filename string, reader io.Reader) string {
	filename = filepath.Join(dir, filepath.Base(filename))
	_, err := os.Stat(filename)
	if err == nil {
		return "File Exists"
	}
	file, err := os.Create(filename)
	if err != nil {
		return "Error"
	}
	defer file.Close()
	_, err = io.CopyBuffer(file, reader, make([]byte, 128*1024))
	if err != nil {
		return "Error"
	}
	return ""
}

func progressBar(total, progress int) {
	barLength := 50
	filledLength := barLength * progress / total
	bar := make([]byte, barLength)
	for i := 0; i < barLength; i++ {
		if i < filledLength {
			bar[i] = '#'
		} else {
			bar[i] = '-'
		}
	}
	percent := progress * 100 / total
	fmt.Printf("\r[%s]%d%%", string(bar), percent)
}

func Handler(dir string) http.Handler {
	return handler(dir)
}
