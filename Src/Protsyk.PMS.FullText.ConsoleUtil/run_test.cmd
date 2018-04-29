@echo off

del index-*.pms

IF "%1"=="" (
	echo "Please provide folder with .txt files to index"
	goto EXIT
)

dotnet Protsyk.PMS.FullText.ConsoleUtil.dll index --input "%1" --fieldsType BTree
dotnet Protsyk.PMS.FullText.ConsoleUtil.dll print

dotnet Protsyk.PMS.FullText.ConsoleUtil.dll search --query "WORD(pms)"

dotnet Protsyk.PMS.FullText.ConsoleUtil.dll lookup --pattern pet*
dotnet Protsyk.PMS.FullText.ConsoleUtil.dll lookup --pattern projct~1

:EXIT