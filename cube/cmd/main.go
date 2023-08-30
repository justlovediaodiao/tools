package main

import (
	"cube"
	"flag"
	"fmt"
)

func main() {
	var u, f, l, r, d, b string
	flag.StringVar(&u, "u", "WWWW", "colors of up face")
	flag.StringVar(&f, "f", "RRRR", "colors of front face")
	flag.StringVar(&l, "l", "GGGG", "colors of left face")
	flag.StringVar(&r, "r", "BBBB", "colors of right face")
	flag.StringVar(&d, "d", "YYYY", "colors of down face")
	flag.StringVar(&b, "b", "OOOO", "colors of back face")
	flag.Parse()

	c := cube.Cube{
		cube.Color(u[0]), cube.Color(u[1]), cube.Color(u[2]), cube.Color(u[3]),
		cube.Color(f[0]), cube.Color(f[1]), cube.Color(f[2]), cube.Color(f[3]),
		cube.Color(l[0]), cube.Color(l[1]), cube.Color(l[2]), cube.Color(l[3]),
		cube.Color(r[0]), cube.Color(r[1]), cube.Color(r[2]), cube.Color(r[3]),
		cube.Color(d[0]), cube.Color(d[1]), cube.Color(d[2]), cube.Color(d[3]),
		cube.Color(b[0]), cube.Color(b[1]), cube.Color(b[2]), cube.Color(b[3]),
	}
	fmt.Println(cube.Recover(&c))
}
