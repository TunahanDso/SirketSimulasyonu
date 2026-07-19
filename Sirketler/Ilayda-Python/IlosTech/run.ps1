$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot
python -m unittest -v
python .\server.py
