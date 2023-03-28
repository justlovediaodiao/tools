package main

import (
	"net"
	"sync"
)

// nat store inside address and outside address(which is net.PacketConn) mapping.
type nat struct {
	mutex *sync.RWMutex
	m     map[string]net.PacketConn
}

func newNAT() *nat {
	return &nat{
		mutex: new(sync.RWMutex),
		m:     make(map[string]net.PacketConn),
	}
}

func (n *nat) Get(key string) net.PacketConn {
	n.mutex.RLock()
	defer n.mutex.RUnlock()
	return n.m[key]
}

func (n *nat) Set(key string, pc net.PacketConn) {
	n.mutex.Lock()
	defer n.mutex.Unlock()

	n.m[key] = pc
}

func (n *nat) Del(key string) net.PacketConn {
	n.mutex.Lock()
	defer n.mutex.Unlock()

	pc, ok := n.m[key]
	if ok {
		delete(n.m, key)
		return pc
	}
	return nil
}
