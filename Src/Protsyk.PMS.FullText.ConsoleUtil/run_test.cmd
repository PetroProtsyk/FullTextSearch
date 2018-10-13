@echo off

del index-*.pms

IF "%1"=="" (
	echo "Please provide folder with .txt files to index"
	goto EXIT
)

dotnet Protsyk.PMS.FullText.ConsoleUtil.dll index --dictionaryType TST --fieldsType BTree --postingType BinaryCompressed --textEncoding UTF-8 --input "%1"
dotnet Protsyk.PMS.FullText.ConsoleUtil.dll print

dotnet Protsyk.PMS.FullText.ConsoleUtil.dll search --query "WORD(pms)"

dotnet Protsyk.PMS.FullText.ConsoleUtil.dll lookup --pattern "WILD(pet*)"
dotnet Protsyk.PMS.FullText.ConsoleUtil.dll lookup --pattern "EDIT(projct, 1)"

:EXIT