' VBScript to create Windows shortcuts
Set objArgs = WScript.Arguments
if objArgs.Count < 3 then
    WScript.Quit(1)
end if

strLinkFile = objArgs(0)
strTargetPath = objArgs(1)
strWorkingDirectory = objArgs(2)

Set objShell = CreateObject("WScript.Shell")
Set objLink = objShell.CreateShortcut(strLinkFile)

objLink.TargetPath = strTargetPath
objLink.WorkingDirectory = strWorkingDirectory
objLink.Description = "Monitor Elite Dangerous gameplay and collect exobiology data"
objLink.Save

WScript.Quit(0)
