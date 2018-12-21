# PMS Full-Text Search Engine for .NET Core
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![Travis Status](https://travis-ci.com/PetroProtsyk/FullTextSearch.svg?branch=master)](https://travis-ci.com/PetroProtsyk/FullTextSearch)

Full-Text Search Engine with no external dependencies written in C# for .NET Core.

The aim of this project is to showcase algorithms, data structures and techniques that are used to create full-text search engines.

# Getting Started

On Windows:

1. Download and build code. Use the following commands:

    ```bat
    dotnet restore
    dotnet build
    ```

2. Open folder with binaries: *bin\Debug\netcoreapp2.0*

3. Start the following command. Replace DATA_PATH with a path to Datasets folder
    ```bat
    run_test.bat DATA_PATH
    ```
4. If everything goes well the following messages are printed:

   Log from index construction:
    ```txt
    dotnet Protsyk.PMS.FullText.ConsoleUtil.dll index --input "F:\Sources\FullTextSearch\Datasets"
    
    PMS Full-Text Search (c) Petro Protsyk 2017-2018
    F:\Sources\FullTextSearch\Datasets\Simple\TestFile001.txt
    F:\Sources\FullTextSearch\Datasets\Simple\TestFile002.txt
    F:\Sources\FullTextSearch\Datasets\Simple\TestFile003.txt
    Indexed documents: 3, time: 00:00:00.1010004
    ```
    
    Dump of the index (for each term in the dictionary - the list of all occurrences):
    ```txt
    dotnet Protsyk.PMS.FullText.ConsoleUtil.dll print
    
    PMS Full-Text Search (c) Petro Protsyk 2017-2018
    2017 -> [1,1,9]
    algorithms -> [1,1,19]
    and -> [1,1,20]
    apple -> [3,1,1]
    banana -> [3,1,2]
    build -> [1,1,25]
    c -> [1,1,16]
    data -> [1,1,21]
    demonstrate -> [1,1,18]
    ...
    ```
    
    Search with query WORD(pms):

    ```txt
    dotnet Protsyk.PMS.FullText.ConsoleUtil.dll search --query "WORD(pms)"
    
    {filename:"TestFile001.txt", size:"180", created:"2018-04-02T10:09:41.4208444+02:00"}
    {[1,1,1]}

    {filename:"TestFile002.txt", size:"29", created:"2018-04-02T10:09:41.4248447+02:00"}
    {[2,1,1]}
    
    Documents found: 2, matches: 2, time: 00:00:00.0564721
    ```
    
    Lookup in the dictionary using a pattern i.e. all terms matching pattern:
    
    ```txt
    dotnet Protsyk.PMS.FullText.ConsoleUtil.dll lookup --pattern "WILD(pet*)"
    petro-mariya-sophie
    Terms found: 1, time: 00:00:00.0704173

    dotnet Protsyk.PMS.FullText.ConsoleUtil.dll lookup --pattern "EDIT(projct, 1)"
    project
    Terms found: 1, time: 00:00:00.0847931
    ```

# Query Language

* WORD(apple)       - single word
* WILD(app*)        - wildcard pattern
* EDIT(apple, 1)    - Levenshtein (edit distance, fuzzy search)

Conjunction operators

* OR                - boolean or
* AND               - boolean and
* SEQ               - sequence of words, phrase

Examples of queries:

* AND(WORD(apple), OR(WILD(a*), EDIT(apple, 1))) 
* SEQ(WORD(hello), WORD(world))

# Data Structures

* Dictionary of the persistent index is implemented using either:
    * [Ternary Search Tree](http://www.protsyk.com/cms/?page_id=3004).
    * [Finite State Transducer (FST)](http://www.protsyk.com/cms/?page_id=3017).
* Key-value storage for document metadata is based on persistent B-Tree implementation: [B-Tree](http://www.protsyk.com/cms/?page_id=3003).

# Algorithms

* Approximate term matching is based on [Levenshtein automaton](http://blog.notdot.net/2010/07/Damn-Cool-Algorithms-Levenshtein-Automata).
* Query Language parser is implemented using [recursive descent parser](https://en.wikipedia.org/wiki/Recursive_descent_parser) technique.
* A method for encoding/decoding occurrences uses [Group VarInt encoding](http://www.ir.uwaterloo.ca/book/addenda-06-index-compression.html).

# References

* [Introduction to Information Retrieval](https://nlp.stanford.edu/IR-book/)

  ![alt text](https://nlp.stanford.edu/IR-book/iir.jpg "Introduction to Information Retrieval")

* [Compilers: Principles, Techniques, and Tools](https://en.wikipedia.org/wiki/Compilers:_Principles,_Techniques,_and_Tools)

  ![alt text](https://upload.wikimedia.org/wikipedia/en/a/a3/Purple_dragon_book_b.jpg "Compilers: Principles, Techniques, and Tools")

* [Information Retrieval: Implementing and Evaluating Search Engines](http://www.ir.uwaterloo.ca/book/)

  ![alt text](http://www.ir.uwaterloo.ca/book/title3.jpg "Information Retrieval: Implementing and Evaluating Search Engines")

# Links

* [Project Website](http://www.protsyk.com/pms)
