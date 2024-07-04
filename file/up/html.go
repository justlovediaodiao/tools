package up

import (
	"strings"
)

var html = `<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="X-UA-Compatible" content="ie=edge">
    <title>文件上传</title>
</head>
<body>
    <form method="post" action="/" enctype="multipart/form-data">
        <input type="file" name="file" multiple>
        <input type="submit" value="提交">
        <p>{tip}</p>
    </form>
</body>
</html>`

func render(tip string) string {
	return strings.Replace(html, "{tip}", tip, 1)
}
