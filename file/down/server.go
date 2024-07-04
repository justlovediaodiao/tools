package down

import (
	"fmt"
	"net"
	"net/http"
)

func wrap(f http.Handler) http.Handler {
	var h = func(w http.ResponseWriter, r *http.Request) {
		w.Header()["Content-Type"] = []string{"application/octet-stream"}
		f.ServeHTTP(w, r)
	}
	return http.HandlerFunc(h)
}

func Serve(listen string, dir string) {
	fmt.Printf("http://%s\n", listen)
	var h = http.FileServer(http.Dir(dir))
	var err = http.ListenAndServe(listen, wrap(h))
	if e, ok := err.(*net.OpError); ok && e.Op == "listen" {
		fmt.Printf("can not listen on %s.\n", listen)
	}
}
