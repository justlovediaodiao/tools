package cube

import (
	"fmt"
)

type Color byte

var (
	W Color = 0 // White
	R Color = 1 // Red
	G Color = 2 // Gren
	B Color = 3 // Blue
	Y Color = 4 // Yellow
	O Color = 5 // Orange
)

type Rotate string

var (
	Lr Rotate = "L+"
	Rr Rotate = "R+"
	Ur Rotate = "U+"
	Dr Rotate = "D+"
	Fr Rotate = "F+"
	Br Rotate = "B+"
	Lc Rotate = "L-"
	Rc Rotate = "R-"
	Uc Rotate = "U-"
	Dc Rotate = "D-"
	Fc Rotate = "F-"
	Bc Rotate = "B-"
)

const N = 2
const NN = N * N

// face order: U F L R D B
// square order: left-bottom right-bottom left-top right-top
type Cube [NN * 6]Color

func NewCube() *Cube {
	c := new(Cube)
	for i := 0; i < NN*6; i++ {
		c[i] = Color(i / NN)
	}
	return c
}

func (c *Cube) Recovered() bool {
	for i := 0; i < NN*6; i += NN {
		face := c[i : i+NN]
		for j := 1; j < NN; j++ {
			if face[0] != face[j] {
				return false
			}
		}
	}
	return true
}

func (c *Cube) Show() {
	for i := 0; i < NN*6; i += NN {
		face := c[i : i+NN]
		c.draw(face[2])
		c.draw(face[3])
		fmt.Println()
		c.draw(face[0])
		c.draw(face[1])
		fmt.Println()
		fmt.Println()
	}
}

func (c *Cube) draw(color Color) {
	switch color {
	case W:
		fmt.Print("\033[97m██\033[0m")
	case Y:
		fmt.Print("\033[93m██\033[0m")
	case R:
		fmt.Print("\033[31m██\033[0m")
	case O:
		fmt.Print("\033[36m██\033[0m") // no Orange, use Cyan instead.
	case B:
		fmt.Print("\033[34m██\033[0m")
	case G:
		fmt.Print("\033[32m██\033[0m")
	}
}

func (c *Cube) U() []Color {
	return c[0:NN]
}

func (c *Cube) F() []Color {
	return c[NN : NN*2]
}

func (c *Cube) L() []Color {
	return c[NN*2 : NN*3]
}

func (c *Cube) R() []Color {
	return c[NN*3 : NN*4]
}

func (c *Cube) D() []Color {
	return c[NN*4 : NN*5]
}

func (c *Cube) B() []Color {
	return c[NN*5 : NN*6]
}

func (c *Cube) Frotate() {
	f := c.F()
	u := c.U()
	l := c.L()
	d := c.D()
	r := c.R()

	f0 := f[0]
	f[0] = f[1]
	f[1] = f[3]
	f[3] = f[2]
	f[2] = f0

	u0 := u[0]
	u1 := u[1]

	u[0] = l[1]
	u[1] = l[3]

	l[1] = d[3]
	l[3] = d[2]

	d[2] = r[0]
	d[3] = r[2]

	r[0] = u1
	r[2] = u0
}

func (c *Cube) FrotateC() {
	f := c.F()
	u := c.U()
	r := c.R()
	d := c.D()
	l := c.L()

	f0 := f[0]
	f[0] = f[2]
	f[2] = f[3]
	f[3] = f[1]
	f[1] = f0

	u0 := u[0]
	u1 := u[1]

	u[0] = r[2]
	u[1] = r[0]

	r[0] = d[2]
	r[2] = d[3]

	d[2] = l[3]
	d[3] = l[1]

	l[1] = u0
	l[3] = u1
}

func (c *Cube) Lrotate() {
	l := c.L()
	u := c.U()
	b := c.B()
	d := c.D()
	f := c.F()

	l0 := l[0]
	l[0] = l[1]
	l[1] = l[3]
	l[3] = l[2]
	l[2] = l0

	u2 := u[2]
	u0 := u[0]

	u[2] = b[1]
	u[0] = b[3]

	b[1] = d[2]
	b[3] = d[0]

	d[0] = f[0]
	d[2] = f[2]

	f[0] = u0
	f[2] = u2
}

func (c *Cube) LrotateC() {
	l := c.L()
	u := c.U()
	f := c.F()
	d := c.D()
	b := c.B()

	l0 := l[0]
	l[0] = l[2]
	l[2] = l[3]
	l[3] = l[1]
	l[1] = l0

	u2 := u[2]
	u0 := u[0]

	u[2] = f[2]
	u[0] = f[0]

	f[2] = d[2]
	f[0] = d[0]

	d[2] = b[1]
	d[0] = b[3]

	b[1] = u2
	b[3] = u0
}

func (c *Cube) Rrotate() {
	r := c.R()
	u := c.U()
	f := c.F()
	d := c.D()
	b := c.B()

	r0 := r[0]
	r[0] = r[1]
	r[1] = r[3]
	r[3] = r[2]
	r[2] = r0

	u3 := u[3]
	u1 := u[1]

	u[3] = f[3]
	u[1] = f[1]

	f[3] = d[3]
	f[1] = d[1]

	d[3] = b[0]
	d[1] = b[2]

	b[2] = u1
	b[0] = u3
}

func (c *Cube) RrotateC() {
	r := c.R()
	u := c.U()
	b := c.B()
	d := c.D()
	f := c.F()

	r0 := r[0]
	r[0] = r[2]
	r[2] = r[3]
	r[3] = r[1]
	r[1] = r0

	u3 := u[3]
	u1 := u[1]

	u[3] = b[0]
	u[1] = b[2]

	b[0] = d[3]
	b[2] = d[1]

	d[3] = f[3]
	d[1] = f[1]

	f[3] = u3
	f[1] = u1
}

func (c *Cube) Brotate() {
	b := c.B()
	u := c.U()
	r := c.R()
	d := c.D()
	l := c.L()

	b0 := b[0]
	b[0] = b[1]
	b[1] = b[3]
	b[3] = b[2]
	b[2] = b0

	u3 := u[3]
	u2 := u[2]

	u[3] = r[1]
	u[2] = r[3]

	r[3] = d[1]
	r[1] = d[0]

	d[0] = l[2]
	d[1] = l[0]

	l[0] = u2
	l[2] = u3
}

func (c *Cube) BrotateC() {
	b := c.B()
	u := c.U()
	l := c.L()
	d := c.D()
	r := c.R()

	b0 := b[0]
	b[0] = b[2]
	b[2] = b[3]
	b[3] = b[1]
	b[1] = b0

	u3 := u[3]
	u2 := u[2]

	u[3] = l[2]
	u[2] = l[0]

	l[2] = d[0]
	l[0] = d[1]

	d[0] = r[1]
	d[1] = r[3]

	r[1] = u3
	r[3] = u2
}

func (c *Cube) Urotate() {
	u := c.U()
	f := c.F()
	r := c.R()
	b := c.B()
	l := c.L()

	u0 := u[0]
	u[0] = u[1]
	u[1] = u[3]
	u[3] = u[2]
	u[2] = u0

	f2 := f[2]
	f3 := f[3]

	f[2] = r[2]
	f[3] = r[3]

	r[2] = b[2]
	r[3] = b[3]

	b[2] = l[2]
	b[3] = l[3]

	l[2] = f2
	l[3] = f3
}

func (c *Cube) UrotateC() {
	u := c.U()
	f := c.F()
	l := c.L()
	b := c.B()
	r := c.R()

	u0 := u[0]
	u[0] = u[2]
	u[2] = u[3]
	u[3] = u[1]
	u[1] = u0

	f2 := f[2]
	f3 := f[3]

	f[2] = l[2]
	f[3] = l[3]

	l[2] = b[2]
	l[3] = b[3]

	b[2] = r[2]
	b[3] = r[3]

	r[2] = f2
	r[3] = f3
}

func (c *Cube) Drotate() {
	d := c.D()
	f := c.F()
	l := c.L()
	b := c.B()
	r := c.R()

	d0 := d[0]
	d[0] = d[1]
	d[1] = d[3]
	d[3] = d[2]
	d[2] = d0

	f0 := f[0]
	f1 := f[1]

	f[0] = l[0]
	f[1] = l[1]

	l[0] = b[0]
	l[1] = b[1]

	b[0] = r[0]
	b[1] = r[1]

	r[0] = f0
	r[1] = f1
}

func (c *Cube) DrotateC() {
	d := c.D()
	f := c.F()
	r := c.R()
	b := c.B()
	l := c.L()

	d0 := d[0]
	d[0] = d[2]
	d[2] = d[3]
	d[3] = d[1]
	d[1] = d0

	f0 := f[0]
	f1 := f[1]

	f[0] = r[0]
	f[1] = r[1]

	r[0] = b[0]
	r[1] = b[1]

	b[0] = l[0]
	b[1] = l[1]

	l[0] = f0
	l[1] = f1
}

func (c *Cube) Rotate(r Rotate) {
	switch r {
	case Lr:
		c.Lrotate()
	case Rr:
		c.Rrotate()
	case Ur:
		c.Urotate()
	case Dr:
		c.Drotate()
	case Fr:
		c.Frotate()
	case Br:
		c.Brotate()
	case Lc:
		c.LrotateC()
	case Rc:
		c.RrotateC()
	case Uc:
		c.UrotateC()
	case Dc:
		c.DrotateC()
	case Fc:
		c.FrotateC()
	case Bc:
		c.BrotateC()
	}
}
