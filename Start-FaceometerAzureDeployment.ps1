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


$prov = .\kiwiot.faceometer.azurerm\deploy.ps1 -subscriptionId $sub.subscriptionid -resourceGroupName $ResourceGroupName -resourceGroupLocation $ResourceGroupLocation -deploymentName "faceometer2" -templateFilePath .\kiwiot.faceometer.azurerm\template.json -parametersFilePath .\kiwiot.faceometer.azurerm\parameters.json -Verbose

#build kudu url
"$($prov.outputs.functionAppName.Value)"

#build function for deployment object
$url = $prov.outputs.functionAppName.Value
$triggers = get-content .\Kiwiot.Faceometer.AzureFunctions\iot_gateway\function.json | convertfrom-json
$function = (Get-content ".\Kiwiot.Faceometer.AzureFunctions\iot_gateway\run.csx" -Raw).ToString()
$testjson = (Get-content ".\Kiwiot.Faceometer.AzureFunctions\iot_gateway\test.json" -Raw).ToString()
$files = @{"run.csx"=$function}
$prop = New-object PSObject -Prop @{ "config"=$triggers; "files"=$files; "test_data"=$testjson}

New-AzureRmResource -ResourceGroupName $ResourceGroupName -ResourceType "Microsoft.Web/sites/functions" -ResourceName "$url/iot_gateway" -PropertyObject $prop -ApiVersion 2015-08-01 -Force