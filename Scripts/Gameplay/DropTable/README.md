## Contents
### DropTable  
Call `GenerateDrops` from this script to generate drops from all other Drop scripts attached to the same object

### Drop
Abstract class for other drop method

### DropSimple
Drop [min, max] amounts of item, chances are uniform

### DropQuanVary
Drop specific amount of item with certain chance. E.g. 70% chance to drop 1 berry, 30% chance to drop 2 berry

### DropTypeVary
Drop one item from the pool with certain chance. E.g. 50% chance to drop stone, 30% chance to drop iron ore, 20% chance to drop gold ore
