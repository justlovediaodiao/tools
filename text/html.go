package main

import (
	"strings"
)

var html = `<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="X-UA-Compatible" content="ie=edge">
    <title>文本剪切板</title>
</head>
<body>
    <form method="post" action="/">
        <textarea name="text" style="width: 550px; height: 300px;">{text}</textarea>
        <input type="submit" value="粘贴">
    </form>
</body>
</html>`

func render(tip string) string {
	return strings.Replace(html, "{text}", tip, 1)
}
