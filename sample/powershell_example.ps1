#!/bin/pwsh
Add-Type -Path 'bin/Debug/net7.0/ScottPlot.dll'
[double[]] $dataX = @( 1, 2, 3, 4, 5 )
[double[]] $dataY = @( 1, 4, 9, 16, 25 )
$plt = [ScottPlot.Plot]::new(400, 300 )
[Void] $plt.AddScatter($dataX, $dataY)
[Void] $plt.SaveFig("$pwd\powershell_example.png")
