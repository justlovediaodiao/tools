package dnsparser

import (
	"fmt"
)

type Flag struct {
	QueryResponseFlag       byte
	OperationCode           byte
	AuthoritativeAnswerFlag byte
	TruncationFlag          byte
	RecursionDesired        byte
	RecursionAvailable      byte
	ResponseCode            byte
}

func (f Flag) OperationCodeString() string {
	switch f.OperationCode {
	case 0:
		return "QUERY"
	case 1:
		return "IQUERY"
	case 2:
		return "STATUS"
	case 4:
		return "NOTIFY"
	case 5:
		return "UPDATE"
	default:
		return "UNKOWN"
	}
}

func (f Flag) ResponseCodeString() string {
	switch f.ResponseCode {
	case 0:
		return "No Error"
	case 1:
		return "Format Error"
	case 2:
		return "Server Failure"
	case 3:
		return "Name Error"
	case 4:
		return "Not Implemented"
	case 5:
		return "Refused"
	case 6:
		return "YX Domain"
	case 7:
		return "YX RR Set"
	case 8:
		return "NX RR Set"
	case 9:
		return "Not Auth"
	case 10:
		return "Not Zone"
	default:
		return "UNKOWN"
	}
}

type Header struct {
	Identifer             uint16
	Flag                  Flag
	QuestionCount         uint16
	AnswerRecordCount     uint16
	AuthorityRecordCount  uint16
	AdditionalRecordCount uint16
}

type Question struct {
	Name  string
	Type  uint16
	Class uint16
}

func (q Question) TypeString() string {
	switch q.Type {
	case 1:
		return "A"
	case 2:
		return "NS"
	case 5:
		return "CNAME"
	case 6:
		return "SOA"
	case 12:
		return "PTR"
	case 15:
		return "MX"
	case 16:
		return "TXT"
	case 251:
		return "IXFR"
	case 252:
		return "AXFR"
	case 253:
		return "MAILB"
	case 254:
		return "MAILA"
	case 255:
		return "*"
	default:
		return "UNKOWN"
	}
}

func (q Question) ClassString() string {
	switch q.Class {
	case 1:
		return "IN"
	case 255:
		return "ANY"
	default:
		return "UNKOWN"
	}
}

type ResourceRecord struct {
	Question
	TTL                uint32
	ResourceDataLength uint16
	ResourceData       []byte
}

func (r ResourceRecord) ResourceDataString() string {
	switch r.Type {
	case 1:
		return fmt.Sprintf("%d.%d.%d.%d", r.ResourceData[0], r.ResourceData[1], r.ResourceData[2], r.ResourceData[3])
	case 2, 5:
		name, _ := parseDNSName(r.ResourceData)
		return name
	default: // todo
		return "NOT SUPPORTED"
	}
}

type DNSMessage struct {
	Header            Header
	Questions         []Question
	AnswerRecords     []ResourceRecord
	AuthorityRecords  []ResourceRecord
	AdditionalRecords []ResourceRecord
}

func (m *DNSMessage) String() string {
	var result string
	op := m.Header.Flag.OperationCodeString()
	for _, q := range m.Questions {
		line := op + ":\t" + q.Name + "\t" + q.TypeString() + "\t" + q.ClassString()
		if result == "" {
			result = line
		} else {
			result += "\n" + line
		}
	}
	if m.Header.Flag.QueryResponseFlag == 0 {
		return result
	}

	if m.Header.Flag.ResponseCode != 0 {
		result += "\nANSWER:\t" + m.Header.Flag.ResponseCodeString()
		return result
	}

	for _, r := range m.AnswerRecords {
		line := "ANSWER:\t" + r.Name + "\t" + r.ResourceDataString() + "\t" + r.TypeString() + "\t" + r.ClassString()
		if result == "" {
			result = line
		} else {
			result += "\n" + line
		}
	}
	for _, r := range m.AuthorityRecords {
		line := "AUTHORITY:\t" + r.Name + "\t" + r.ResourceDataString() + "\t" + r.TypeString() + "\t" + r.ClassString()
		if result == "" {
			result = line
		} else {
			result += "\n" + line
		}
	}
	for _, r := range m.AdditionalRecords {
		line := "ADDITIONAL:\t" + r.Name + "\t" + r.ResourceDataString() + "\t" + r.TypeString() + "\t" + r.ClassString()
		if result == "" {
			result = line
		} else {
			result += "\n" + line
		}
	}
	return result
}

type parser struct {
	packet []byte
	pos    int
}

func Parse(packet []byte) *DNSMessage {
	var msg DNSMessage
	p := parser{packet, 0}
	header := p.parseHeader()
	msg.Header = header
	if header.QuestionCount > 0 {
		msg.Questions = make([]Question, int(header.QuestionCount))
		for i := 0; i < int(header.QuestionCount); i++ {
			msg.Questions[i] = p.parseQuestion()
		}
	}
	if header.AnswerRecordCount > 0 {
		msg.AnswerRecords = make([]ResourceRecord, int(header.AnswerRecordCount))
		for i := 0; i < int(header.AnswerRecordCount); i++ {
			msg.AnswerRecords[i] = p.parseResourceRecord()
		}
	}
	if header.AuthorityRecordCount > 0 {
		msg.AuthorityRecords = make([]ResourceRecord, int(header.AuthorityRecordCount))
		for i := 0; i < int(header.AuthorityRecordCount); i++ {
			msg.AuthorityRecords[i] = p.parseResourceRecord()
		}
	}
	if header.AdditionalRecordCount > 0 {
		msg.AdditionalRecords = make([]ResourceRecord, int(header.AdditionalRecordCount))
		for i := 0; i < int(header.AdditionalRecordCount); i++ {
			msg.AdditionalRecords[i] = p.parseResourceRecord()
		}
	}
	return &msg
}

func bytesToU16(b []byte) uint16 {
	return uint16(b[0])<<8 | uint16(b[1])
}

func bytesToU32(b []byte) uint32 {
	return uint32(b[0])<<24 | uint32(b[1])<<16 | uint32(b[2])<<8 | uint32(b[3])
}

func bitTobyte(b byte, s, e int) byte {
	return (b >> (8 - e)) & (1<<(e-s) - 1)
}

func (p *parser) parseHeader() Header {
	var header Header
	b := p.packet
	header.Identifer = bytesToU16(b[:2])
	header.Flag = parseFlag(b[2:4])
	header.QuestionCount = bytesToU16(b[4:6])
	header.AnswerRecordCount = bytesToU16(b[6:8])
	header.AuthorityRecordCount = bytesToU16(b[8:10])
	header.AdditionalRecordCount = bytesToU16(b[10:12])
	p.pos = 12
	return header
}

func parseFlag(b []byte) Flag {
	var flag Flag
	flag.QueryResponseFlag = bitTobyte(b[0], 0, 1)
	flag.OperationCode = bitTobyte(b[0], 1, 5)
	flag.AuthoritativeAnswerFlag = bitTobyte(b[0], 5, 6)
	flag.TruncationFlag = bitTobyte(b[0], 6, 7)
	flag.RecursionDesired = bitTobyte(b[0], 7, 8)
	flag.RecursionAvailable = bitTobyte(b[1], 0, 1)
	flag.ResponseCode = bitTobyte(b[1], 4, 8)
	return flag
}

func (p *parser) parseQuestion() Question {
	var question Question
	question.Name = p.parseDNSName()
	b := p.packet[p.pos:]
	question.Type = bytesToU16(b[:2])
	question.Class = bytesToU16(b[2:4])
	p.pos += 4
	return question
}

func (p *parser) parseDNSName() string {
	b := p.packet[p.pos:]
	if b[0] >= 192 {
		name, _ := parseDNSName(p.packet[int(b[1]):])
		p.pos += 2
		return name
	}
	name, n := parseDNSName(b)
	p.pos += n
	return name
}

func parseDNSName(b []byte) (string, int) {
	var name string
	var i int
	for {
		length := b[i]
		i++
		if length == 0 {
			break
		}
		end := i + int(length)
		if name != "" {
			name += "."
		}
		name += string(b[i:end])
		i = end
	}
	return name, i
}

func (p *parser) parseResourceRecord() ResourceRecord {
	var record ResourceRecord
	record.Question = p.parseQuestion()
	b := p.packet[p.pos:]
	record.TTL = bytesToU32(b[:4])
	length := bytesToU16(b[4:6])
	record.ResourceDataLength = length
	record.ResourceData = b[6 : 6+length]
	p.pos += 6 + int(length)
	return record
}
