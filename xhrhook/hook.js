async function registScript() {
    let script = document.createElement('script');
    script.src = "https://unpkg.com/ajax-hook@2.0.3/dist/ajaxhook.min.js";
    document.getElementsByTagName('head')[0].appendChild(script);
    while (typeof(ah) === 'undefined') {
        await new Promise(reslove => setTimeout(reslove, 1000));
    }
}

// send request by XMLHttpRequest.
function send(req) {
    let xhr = new window['_rxhr']();    // this is the real XMLHttpRequest
    xhr.withCredentials = req.withCredentials;
    xhr.open(req.method, req.url, req.async !== false, req.user, req.password);
    for (var key in req.headers) {
        xhr.setRequestHeader(key, req.headers[key]);
    }
    xhr.send(req.body);
}

function onRequest(req, handler) {
    console.log(req);
    handler.next(req);
}

function onResponse(res, handler) {
    console.log(res);
    handler.next(res);
}

function onError(err, handler) {
    console.log(err);
    handler.next(err);
}

await registScript();
ah.proxy({onRequest, onResponse, onError});
