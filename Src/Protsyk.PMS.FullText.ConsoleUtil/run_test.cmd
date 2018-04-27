@echo off

del index-*.pms

IF "%1"=="" (
	echo "Please provide folder with .txt files to index"
	goto EXIT
)

dotnet Protsyk.PMS.FullText.ConsoleUtil.dll index %1 --fieldsType BTree
dotnet Protsyk.PMS.FullText.ConsoleUtil.dll print

dotnet Protsyk.PMS.FullText.ConsoleUtil.dll search "WORD(pms)"

dotnet Protsyk.PMS.FullText.ConsoleUtil.dll lookup pet*
dotnet Protsyk.PMS.FullText.ConsoleUtil.dll lookup projct~1

:EXIT