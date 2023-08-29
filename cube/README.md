# Cube

It's used to recover 2x2 magic cube.

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