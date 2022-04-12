# CascadeAsyncifier

A Roslyn-powered tool to transitively generate asynchronous methods in C# projects.

## What does it do?

This tool searches for methods with async overload. 
Then every method in source code that doesn't have async overload but calls a method that has one is duplicated and transformed into asynchronous version, with every await call possible.
Then the same is done for every method that called it, and every method that called them, etc. Transformed sync methods that are no longer used are deleted. 

## Why do I need this?

If your project is mostly synchronous, when you rewrite a method into its async version, you need to do the same with every method that uses it directly or through another method(s).
CascadeAsyncifier does most of this routine automatically, leaving you to take care of more complicated things such as lambdas.

## Usage

All of the parameters are optional, simply drop CascadeAsyncifier.exe in solution directory and launch it from there.

* `--solution-path <path>` Path to the solution. By default, uses one of the \*.sln files in current directory.
* `--msbuild-path <path>` Path to the MSBuild folder. By default, will ask which one to use if more than one is detected.
* `--target-framework <target>` If solution projects target multiple frameworks, the tool needs to know which one to use. This parameter will be passed as MSBuild argument. 
* `--configure-await-false` If this flag is specified, CascadeAsyncifier will add .ConfigureAwait(false) to _every_ await expression.
* `--omit-async-await` If async method's only await expression is its last statement (either as await task; or return await task;) then tool will remove \"async\" and \"await\" keywords, passing underlying Task expression as a return value.
* `--starting-file-path-regex <regex>` Specify which files' methods will be asyncified. Only affects initial selection of methods, if these methods are used by methods in files that do not match regular expression, they will still be asyncified.     
