package text

import (
	"net/http"
)

func handler() http.HandlerFunc {
	var text = ""
	return func(w http.ResponseWriter, r *http.Request) {
		var res string
		switch r.Method {
		case "GET":
			res = render(text)
		case "POST":
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
		default:
			w.WriteHeader(405)
			res = "405 Method Not Allowed"
		}
		w.Write([]byte(res))
	}
}

func Handler() http.Handler {
	return handler()
}
