# PMS Full-Text search engine for .NET Core
Simple Full-Text search engine with no external dependencies written in C#.

# Query Language

* WORD(apple)       - single word
* WILD(app*)        - wildcard pattern
* EDIT(apple, 1)    - Levenshtein (edit distance, fuzzy search)

Conjunction operators

* OR                - boolean or
* SEQ               - sequence of words, phrase

Examples of queries:

* AND(WORD(apple), OR(WILD(a*), EDIT(apple, 1))) 
* SEQ(WORD(hello), WORD(world))

# Data Structures

Dictionary of the persistent index is implemented using: [Trie and Ternary Search Tree](http://www.protsyk.com/cms/?page_id=3004)

# Algorithms

Approximate term matching is based on [Levenshtein automaton](http://blog.notdot.net/2010/07/Damn-Cool-Algorithms-Levenshtein-Automata)
