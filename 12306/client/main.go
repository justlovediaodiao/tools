package main

import (
	"encoding/json"
	"fmt"
	"io/ioutil"
	"net"
	"net/http"
	"net/url"
	"strings"
	"time"
)

var (
	host  = "https://kyfw.12306.cn/otn"
	path  = "leftTicket/queryA"
	query = "leftTicketDTO.train_date=2020-01-12&leftTicketDTO.from_station=SZQ&leftTicketDTO.to_station=WHN&purpose_codes=ADULT"
)

func checkIP() {
	// no redirect
	var client = &http.Client{
		CheckRedirect: func(req *http.Request, via []*http.Request) error {
			return http.ErrUseLastResponse
		},
	}
	for {
		var req, _ = http.NewRequest("GET", fmt.Sprintf("%s/%s?%s", host, path, query), nil)
		// need cookie
		req.Header.Set("Cookie", "_jc_save_fromStation=%u6DF1%u5733%2CSZQ")
		resp, err := client.Do(req)
		if err != nil {
			fmt.Printf("check ip failed, %v\n", err)
			return
		}
		defer resp.Body.Close()
		// need to hack go net/http/client.go, return resp but not error when status code is 302 and location header not exists.
		if resp.StatusCode == http.StatusFound && resp.Header.Get("Location") == "" && strings.HasPrefix(resp.Header.Get("Content-Type"), "application/json") {
			content, err := ioutil.ReadAll(resp.Body)
			if err != nil {
				fmt.Printf("check ip failed, %v\n", err)
				return
			}
			var r = struct {
				Curl string `json:"c_url"`
			}{}
			if err = json.Unmarshal(content, &r); err != nil {
				fmt.Printf("check ip failed, %v\n", err)
				return
			}
			if r.Curl == "" {
				fmt.Printf("check ip failed, %s\n", string(content))
				return
			}
			path = r.Curl
			fmt.Printf("switch path to %s\n", path)
			continue
		}
		// check status code and resp json httpstatus=200
		if resp.StatusCode != http.StatusOK {
			switchIP()
			return
		}
		content, err := ioutil.ReadAll(resp.Body)
		if err != nil {
			fmt.Printf("check ip failed, %v\n", err)
			return
		}
		var r = struct {
			HttpStatus int `json:"httpstatus"`
		}{}
		if err = json.Unmarshal(content, &r); err != nil {
			fmt.Printf("check ip failed, %v\n", err)
			return
		}
		if r.HttpStatus != 200 {
			switchIP()
		}
		// success
		return
	}
}

func switchIP() {
	// login into router
	var values = url.Values{
		"username": []string{"admin"},
		"Pwd":      []string{"bitcoin $100000"},
	}
	resp, err := http.PostForm("http://192.168.1.1/login.cgi", values)
	if err != nil {
		fmt.Printf("login router failed, %v\n", err)
		return
	}
	defer resp.Body.Close()
	content, err := ioutil.ReadAll(resp.Body)
	if err != nil {
		fmt.Printf("login router failed, %v\n", err)
		return
	}
	var r = struct {
		ErrorCode int `json:"error_code"`
	}{}
	if err = json.Unmarshal(content, &r); err != nil {
		fmt.Printf("login router failed, %v\n", err)
		return
	}
	if r.ErrorCode != 0 {
		fmt.Printf("login router failed, %s\n", string(content))
		return
	}
	// disconnect
	values = url.Values{
		"protocol":   []string{"pppoe"},
		"connection": []string{"0"},
	}
	resp, err = http.PostForm("http://192.168.1.1/network_connect.cgi", values)
	if err != nil {
		fmt.Printf("disconnect failed, %v\n", err)
		return
	}
	resp.Body.Close()
	// wait 2 minute to avoid being assigned to the old IP
	time.Sleep(time.Minute * 2)
	// connect
	values = map[string][]string{
		"protocol":   []string{"pppoe"},
		"connection": []string{"1"},
	}
	resp, err = http.PostForm("http://192.168.1.1/network_connect.cgi", values)
	if err != nil {
		fmt.Printf("connect failed, %v\n", err)
		return
	}
	resp.Body.Close()
	fmt.Println("switch ip success")
}

func reportIP() {
	// send a udp string "justlovediaodiao" to justlovediaodiao.com:1333
	addr, err := net.ResolveUDPAddr("udp", "justlovediaodiao.com:1333")
	if err != nil {
		fmt.Printf("report ip failed, %v", err)
		return
	}
	conn, err := net.DialUDP("udp", nil, addr)
	if err != nil {
		fmt.Printf("report ip failed, %v", err)
		return
	}
	defer conn.Close()
	n, err := conn.Write([]byte("justlovediaodiao"))
	if n != 16 || err != nil {
		fmt.Printf("report ip failed, %d, %v\n", n, err)
	}
}

func main() {
	for {
		time.Sleep(time.Minute * 1)
		go checkIP()
		time.Sleep(time.Minute * 1)
		go reportIP()
	}
}
