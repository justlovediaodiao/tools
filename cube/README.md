# Cube

It's used to recover 2x2 magic cube.

## Usage

```
go build -o cube ./cmd
```

```
Usage of ./cube:
  -b string
        colors of back face (default "OOOO")
  -d string
        colors of down face (default "YYYY")
  -f string
        colors of front face (default "RRRR")
  -l string
        colors of left face (default "GGGG")
  -r string
        colors of right face (default "BBBB")
  -u string
        colors of up face (default "WWWW")
```

Use by go code:

```go
import "fmt"

func main() {
    c := Cube{
        // left-bottom, right-bottom, left-top, right-top
		R, W, G, G, // up
		O, Y, B, R, // front
		R, B, R, Y, // left
		O, W, B, W, // right
		G, B, Y, G, // down
		O, W, O, Y, // back
	}
	fmt.Println(Recover(&c))
    // F+ F+ R- U+ F- U+ F+ F+ R+ R+ U+

    // F: front, R: right, U: up.
    // + means rotate 90 degrees clockwise.
    // - means rotate 90 degress counter clockwise.
}
```