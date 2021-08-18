package main

import (
	"crypto/md5"
	"flag"
	"fmt"
	"net/http"
	"os"
	"strings"
	"text/template"
	"time"
)

var (
	listen   = "127.0.0.1:4396"
	dir      = "static"
	password = "bitcoin $100000"
	sid      string
	sidTime  int64
	tmpl     *template.Template
)

func authMiddleware(h func(w http.ResponseWriter, r *http.Request)) func(w http.ResponseWriter, r *http.Request) {
	var f = func(w http.ResponseWriter, r *http.Request) {
		if sidTime != getSidTime() {
			setNewSid()
		}
		var cookie = r.Header.Get("Cookie")
		// maybe not strict
		if strings.Index(cookie, sid) == -1 {
			if strings.HasPrefix(r.URL.Path, "/file") {
				w.WriteHeader(403)
				w.Write(nil)
			} else {
				redict(w, "/login")
			}
			return
		}
		h(w, r)
	}
	return f
}

func redict(w http.ResponseWriter, url string) {
	w.Header().Set("Location", url)
	w.WriteHeader(301)
	w.Write(nil)
}

func login(w http.ResponseWriter, r *http.Request) {
	tmpl.ExecuteTemplate(w, "login.html", nil)
}

func auth(w http.ResponseWriter, r *http.Request) {
	if r.ParseForm() != nil {
		redict(w, "/login")
		return
	}
	var pass = r.PostForm.Get("password")
	if pass != password {
		redict(w, "/login")
		return
	}
	w.Header().Set("Set-Cookie", fmt.Sprintf("sid=%s; path=/; max-age=2592000", sid))
	redict(w, "/")
}

func index(w http.ResponseWriter, r *http.Request) {
	files, err := os.ReadDir(dir)
	if err != nil {
		w.WriteHeader(500)
		w.Write(nil)
		return
	}
	var names = make([]string, 0, len(files))
	for _, f := range files {
		if f.IsDir() {
			continue
		}
		names = append(names, f.Name())
	}
	tmpl.ExecuteTemplate(w, "index.html", names)
}

func file(w http.ResponseWriter, r *http.Request) {
	var name = r.URL.Query().Get("name")
	if name == "" || strings.IndexAny(name, `/\<>|*:?"`) != -1 {
		w.WriteHeader(404)
		w.Write(nil)
		return
	}
	w.Header().Set("X-Accel-Redirect", "/static/"+name)
	w.Write(nil)
}

func getSidTime() int64 {
	return time.Now().Unix() / 2592000 * 2592000
}

func setNewSid() error {
	var seconds = getSidTime()
	var m = md5.New()
	_, err := m.Write([]byte(fmt.Sprintf("bitcoin must go to $100000!%d", seconds)))
	if err != nil {
		return err
	}
	var hash = m.Sum(nil)

	sid = fmt.Sprintf("%x", hash)
	sidTime = seconds
	return nil
}

func start() {
	if err := setNewSid(); err != nil {
		panic(err)
	}

	http.HandleFunc("/", authMiddleware(index))
	http.HandleFunc("/login", login)
	http.HandleFunc("/auth", auth)
	http.HandleFunc("/file", authMiddleware(file))

	t, err := template.ParseFiles("index.html", "login.html")
	if err != nil {
		panic(err)
	}
	tmpl = t

	http.ListenAndServe(listen, nil)
}

func main() {
	flag.StringVar(&listen, "l", listen, "server listen address")
	flag.StringVar(&dir, "d", dir, "file directory")
	flag.StringVar(&password, "p", password, "login password")
	flag.Parse()
	start()
}
