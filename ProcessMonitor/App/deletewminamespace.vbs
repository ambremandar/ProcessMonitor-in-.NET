strComputer = "SKLTAmbreM"

Set objWMIService = GetObject("winmgmts:\\" & strComputer & "\root")

Set objItem = objWMIService.Get("__Namespace.Name='AmbreCorp'")
objItem.Delete_