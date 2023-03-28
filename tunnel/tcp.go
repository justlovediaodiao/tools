package main

import (
	"errors"
	"io"
	"log"
	"net"
	"os"
	"time"
)

func startTCP(laddr string, taddr string) error {
	l, err := net.Listen("tcp", laddr)
	if err != nil {
		return err
	}
	log.Printf("tcp tunnel %s <----> %s", laddr, taddr)
	for {
		conn, err := l.Accept()
		if err != nil {
			log.Printf("accept error: %v", err)
			continue
		}
		go handleStream(conn, taddr)
	}
}

func handleStream(conn net.Conn, taddr string) {
	defer conn.Close()
	rc, err := net.Dial("tcp", taddr)
	if err != nil {
		log.Printf("dial to %s error: %v", taddr, err)
		return
	}
	defer rc.Close()
	log.Printf("tcp %s <----> %s <----> %s", conn.RemoteAddr().String(), conn.LocalAddr().String(), rc.RemoteAddr().String())
	err = relayStream(conn, rc)
	if err != nil {
		log.Printf("relay stream error: %v", err)
	}
}

// relayStream copy between left and right.
func relayStream(left, right net.Conn) error {
	done := make(chan error, 1)
	go func() {
		_, err := io.Copy(right, left)
		done <- err
		right.SetReadDeadline(time.Now()) // unblock read on right
	}()

	_, err := io.Copy(left, right)
	left.SetReadDeadline(time.Now()) // unblock read on left

	// ignore timeout error.
	err1 := <-done
	if !errors.Is(err, os.ErrDeadlineExceeded) {
		return err
	}
	if !errors.Is(err1, os.ErrDeadlineExceeded) {
		return err1
	}
	return nil
}
