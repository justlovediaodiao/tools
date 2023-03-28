package main

import (
	"os"
	"testing"

	dnsparser "github.com/justlovediaodiao/dns-parser"
)

func TestQueryParser(t *testing.T) {
	b, _ := os.ReadFile("dnsq")
	msg := dnsparser.Parse(b)
	t.Log(msg)
}

func TestResponseParser(t *testing.T) {
	b, _ := os.ReadFile("dnsr")
	msg := dnsparser.Parse(b)
	t.Log(msg)
}
