package main

import (
	"flag"
	"log"
)

func main() {
	var laddr, taddr string
	var tcp, udp bool
	flag.StringVar(&laddr, "l", "", "listen address")
	flag.StringVar(&taddr, "t", "", "target address")
	flag.BoolVar(&tcp, "tcp", false, "start tcp tunnel")
	flag.BoolVar(&udp, "udp", false, "start udp tunnel")
	flag.Parse()
	if laddr == "" || taddr == "" || (!tcp && !udp) {
		flag.Usage()
		return
	}
	if tcp && udp {
		go func() {
			var err = startTCP(laddr, taddr)
			if err != nil {
				log.Print(err)
			}
		}()
	} else if tcp {
		var err = startTCP(laddr, taddr)
		if err != nil {
			log.Print(err)
		}
	}
	if udp {
		var err = startUDP(laddr, taddr)
		if err != nil {
			log.Print(err)
		}
	}
}
