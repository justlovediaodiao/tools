package cube

type stateSets map[Cube]struct{}

func (s stateSets) Add(c *Cube) bool {
	if _, ok := s[*c]; ok {
		return false
	}
	s[*c] = struct{}{}
	return true
}

var (
	states  = make(stateSets)
	rotates = []Rotate{Rr, Fr, Ur, Rc, Fc, Uc}
)

type step struct {
	cube *Cube
	path string
}

func visit(pre []*step) []*step {
	next := make([]*step, 0, len(pre)*len(rotates))
	for _, s := range pre {
		for _, r := range rotates {
			cc := *s.cube
			cc.Rotate(r)
			if states.Add(&cc) {
				next = append(next, &step{&cc, s.path + string(r)})
			}
		}
	}
	return next
}

func Recover(c *Cube) string {
	cur := []*step{{c, ""}}
	// any 2x2 cube can be recovered in 16 steps (only rotate right,front,up faces).
	for i := 0; i < 16; i++ {
		cur = visit(cur)
		for _, s := range cur {
			if s.cube.Recovered() {
				return s.path
			}
		}
	}
	panic("this cube cannot be recovered")
}
