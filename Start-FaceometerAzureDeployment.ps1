param(
 [Parameter(Mandatory=$True)]
 [string]
 $ResourceGroupName,

 [string]
 $ResourceGroupLocation
)

#select subscription
$sub = Get-AzureRmSubscription -ErrorAction SilentlyContinue
If ($sub -eq $null) { Login-AzureRmAccount -ErrorAction Stop }
$sub = Get-AzureRmSubscription | Where State -eq "Enabled"

If ($sub.Count -ne $null -and $sub.Count -gt 1) {
    $sub = $sub[0]
}

"Provisioning Azure Resources"
"Resource group name: $ResourceGroupName"
$prov = .\kiwiot.faceometer.azurerm\deploy.ps1 -subscriptionId $sub.subscriptionid -resourceGroupName $ResourceGroupName -resourceGroupLocation $ResourceGroupLocation -deploymentName $ResourceGroupLocation -templateFilePath .\kiwiot.faceometer.azurerm\template.json -parametersFilePath .\kiwiot.faceometer.azurerm\parameters.json

#build kudu url
#"$($prov.outputs.functionAppName.Value)"

#build function for deployment object
"Uploading code into azure function:  $($prov.outputs.functionAppName.Value).azurewebsites.net"
$functions = @("faceometer_gateway", "emotion_gateway")
$functions | % {
    $functionName = $_
    $basePath = ".\Kiwiot.Faceometer.AzureFunctions\$functionName"
    $url = $prov.outputs.functionAppName.Value

    $triggers = get-content "$basePath\function.json" | convertfrom-json
    $testjson = (Get-content "$basePath\test.json" -Raw).ToString()
    $files = @{"run.csx"=(Get-content "$basePath\run.csx" -Raw).ToString()}
    $prop = New-object PSObject -Prop @{ "config"=$triggers; "files"=$files; "test_data"=$testjson}

    $result = New-AzureRmResource -ResourceGroupName $ResourceGroupName -ResourceType "Microsoft.Web/sites/functions" -ResourceName "$url/$functionName" -PropertyObject $prop -ApiVersion 2015-08-01 -Force

    "Retrieving url trigger URL`r`n`r`n"
    $secrets = Invoke-AzureRmResourceAction -ResourceGroupName $resourcegroupname -ResourceType "Microsoft.Web/sites/functions" -ResourceName "$url/$functionName" -Action listSecrets -ApiVersion "2016-03-01" -Force

    $secrets | fl
}