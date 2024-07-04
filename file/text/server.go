package text

import (
	"fmt"
	"net"
	"net/http"
)

var text = ""

func handle(w http.ResponseWriter, r *http.Request) {
	var res string
	if r.Method == "GET" {
		res = render(text)
	} else if r.Method == "POST" {
		err := r.ParseForm()
		if err != nil {
			res = "Bad Request"
		} else {
			t, ok := r.PostForm["text"]
			if ok {
				text = t[0]
			}
			res = render(text)
		}
	} else {
		w.WriteHeader(405)
		res = "405 Method Not Allowed"
	}
	w.Write([]byte(res))
}

func Serve(listen string) {
	fmt.Printf("http://%s\n", listen)
	http.HandleFunc("/", handle)
	var err = http.ListenAndServe(listen, nil)
	if e, ok := err.(*net.OpError); ok && e.Op == "listen" {
		fmt.Printf("can not listen on %s\n", listen)
	}
}
