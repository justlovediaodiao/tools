package down

import (
	"net/http"
)

func wrap(f http.Handler) http.Handler {
	var h = func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/octet-stream")
		f.ServeHTTP(w, r)
	}
	return http.HandlerFunc(h)
}

func Handler(dir string) http.Handler {
	return wrap(http.FileServer(http.Dir(dir)))
}
