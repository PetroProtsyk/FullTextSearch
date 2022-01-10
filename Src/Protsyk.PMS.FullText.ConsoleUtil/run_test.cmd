rem @echo off

If exist "index-*.pms" (
        del index-*.pms
)

IF "%1"=="" (
	echo Please provide folder with .txt files to index
	goto EXIT
)

Protsyk.PMS.FullText.ConsoleUtil index --dictionaryType TST --fieldsType HashTable --postingType BinaryCompressed --textEncoding UTF-8 --input "%1"
Protsyk.PMS.FullText.ConsoleUtil print

Protsyk.PMS.FullText.ConsoleUtil search --query "WORD(pms)"

Protsyk.PMS.FullText.ConsoleUtil lookup --pattern "WILD(pet*)"
Protsyk.PMS.FullText.ConsoleUtil lookup --pattern "EDIT(projct, 1)"

:EXIT