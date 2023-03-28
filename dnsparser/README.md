# DNS Parser

Parse DNS udp packet.

# Get Started

- Import

```bash
go get github.com/justlovediaodiao/dnsparser
```

- Usage

```go
import "fmt"
import "github.com/justlovediaodiao/dnsparser"

// get dns packet from dump file or udp listener ...
packet, _ := os.ReadFile("") 

msg := dnsparser.Parse(packet)
fmt.Println(msg)
```
