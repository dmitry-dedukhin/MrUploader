<%@ Page Language="C#" AutoEventWireup="true" %>



<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml" >
<head><title>
	MrUploader
</title>
    <style type="text/css">
    html, body {
	    height: 100%;
	    overflow: auto;
    }
    body {
	    padding: 0;
	    margin: 0;
    }
    #silverlightControlHost {
	    height: 100%;
	    text-align:center;
    }
    </style>
    <script type="text/javascript" src="Silverlight.js"></script>
    <script type="text/javascript">
        function onSilverlightError(sender, args) {
            var appSource = "";
            if (sender != null && sender != 0) {
              appSource = sender.getHost().Source;
            }
            
            var errorType = args.ErrorType;
            var iErrorCode = args.ErrorCode;

            if (errorType == "ImageError" || errorType == "MediaError") {
              return;
            }

            var errMsg = "Unhandled Error in Silverlight Application " +  appSource + "\n" ;

            errMsg += "Code: "+ iErrorCode + "    \n";
            errMsg += "Category: " + errorType + "       \n";
            errMsg += "Message: " + args.ErrorMessage + "     \n";

            if (errorType == "ParserError") {
                errMsg += "File: " + args.xamlFile + "     \n";
                errMsg += "Line: " + args.lineNumber + "     \n";
                errMsg += "Position: " + args.charPosition + "     \n";
            }
            else if (errorType == "RuntimeError") {           
                if (args.lineNumber != 0) {
                    errMsg += "Line: " + args.lineNumber + "     \n";
                    errMsg += "Position: " +  args.charPosition + "     \n";
                }
                errMsg += "MethodName: " + args.methodName + "     \n";
            }

            throw new Error(errMsg);
        }
		function test() {
			var slhost = document.getElementById("SLOBJ");
			slhost.content.API.startUpload(window.sessionid, 'http://test.ru/upload/', 'swf=1&upload=1&upmode=contextflash');
			return false;
		}
		function newUploadCallback(fileUploaderId, fileName, fileSize, timestamp, totalSelectedFilesCount) {
			window.sessionid = fileUploaderId;
			setTimeout("test()", 50);
		}
		function UploadFailedCallback(fileUploaderId) {
			//alert("UploadFailedCallback:" + fileUploaderId);
		}
		function UploadDataCallback(fileUploaderId, serverData, filesize) {
			document.getElementById('progress').innerHTML = '0%';
			//alert("UploadDataCallback:" + fileUploaderId + '['+filesize+']: '+ serverData);
		}
		function UploadDoneCallback(fileUploaderId) {
			//alert("UploadDoneCallback:" + fileUploaderId);
		}
		function UploadProgressCallback(fileUploaderId, bytesUploaded, bytesTotal) {
			document.getElementById('progress').innerHTML = parseInt(100 * bytesUploaded / bytesTotal) + '%';
			//alert("UploadProgressCallback:" + fileUploaderId);
		}
		function UploadPluginReady(ver, istimer, trycount) {
			//alert("UploadPluginReady: ver="+ver+", istimer="+istimer+", trycount="+trycount);
		}
		function cancelUpload() {
			var slhost = document.getElementById("SLOBJ");
			slhost.content.API.cancelUpload(window.sessionid);
		}
    </script>
</head>
<body>
<form name="form1" method="post" action="MrUploaderTestPage.aspx" id="form1" style="height:100%">
	<div>
		<input type="hidden" name="__VIEWSTATE" id="__VIEWSTATE" value="/wEPDwUJNzgzNDMwNTMzZGQpaAl5bgBAfo3eeRKlW9Fto3tZSg==" />
		<div id="progress">0%</div>
		<br/><a href="#" onclick="cancelUpload(); return false;">X</a>
	</div>
    <div id="silverlightControlHost">
        <object id="SLOBJ" data="data:application/x-silverlight-2," type="application/x-silverlight-2" width="100%" height="100%">
		  <param name="source" value="ClientBin/MrUploader.xap"/>
		  <param name="onError" value="onSilverlightError" />
		  <param name="background" value="white" />
		  <param name="minRuntimeVersion" value="3.0.40818.0" />
		  <param name="autoUpgrade" value="true" />
          <param name="initParams" value="BrowseText=Загрузить файлы,buttonURL=http://img.imgsmail.ru/mail/ru/images/files/browse_js_glyph_RU.png" />
	    </object>
	</div>
</form>
</body>
</html>