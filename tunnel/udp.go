package main

import (
	"errors"
	"log"
	"net"
	"os"
	"time"
)

const (
	maxPacketSize      = 65536           // udp packet max size
	relayPacketTimeout = time.Minute * 5 // udp nat timeout
)

func startUDP(laddr string, taddr string) error {
	srvAddr, err := net.ResolveUDPAddr("udp", taddr)
	if err != nil {
		return err
	}
	conn, err := net.ListenPacket("udp", laddr)
	if err != nil {
		return err
	}
	defer conn.Close()
	log.Printf("udp tunnel %s <----> %s", laddr, taddr)
	var nat = newNAT()
	var buf = make([]byte, maxPacketSize)
	for {
		handlePacket(conn, srvAddr, nat, buf)
	}
}

func handlePacket(conn net.PacketConn, taddr net.Addr, nat *nat, buf []byte) {
	n, addr, err := conn.ReadFrom(buf)
	if err != nil {
		log.Printf("read packet error: %v", err)
		return
	}
	// get nat outside address. if none, pick one and relay outside to inside.
	var rc = nat.Get(addr.String())
	if rc == nil {
		rc, err = net.ListenPacket("udp", "")
		if err != nil {
			log.Printf("listen packet error: %v", err)
			return
		}
		nat.Set(addr.String(), rc)
		log.Printf("udp %s <----> %s <----> %s", addr.String(), conn.LocalAddr().String(), taddr.String())
		go func() {
			defer rc.Close()
			var err = relayPacket(rc, conn, addr)
			if err != nil {
				if err, ok := err.(net.Error); ok && err.Timeout() { // ignore network timeout
					return
				}
				log.Printf("relay packet error: %v", err)
			}
			nat.Del(addr.String())
		}()
	}
	// send to remote from outside address.
	_, err = rc.WriteTo(buf[:n], taddr)
	if err != nil {
		log.Printf("write packet error: %v", err)
	}
}

// relayPacket copy packet from left to right until timeout.
func relayPacket(left, right net.PacketConn, addr net.Addr) error {
	var buf = make([]byte, maxPacketSize)
	for {
		left.SetReadDeadline(time.Now().Add(relayPacketTimeout)) // wake up when timeout
		n, _, err := left.ReadFrom(buf)
		if err != nil {
			if errors.Is(err, os.ErrDeadlineExceeded) {
				return nil
			}
			return err
		}
		_, err = right.WriteTo(buf[:n], addr)
		if err != nil {
			return err
		}
	}
}
