# DNS Parser

Parse DNS udp packet.

# Get Started

- Import

```bash
go get github.com/justlovediaodiao/dns-parser
```

- Usage

```go
import "fmt"
import dnsparser "github.com/justlovediaodiao/dns-parser"

// get dns packet from dump file or udp listener ...
packet, _ := os.ReadFile("") 

msg := dnsparser.Parse(packet)
fmt.Println(msg)
```
