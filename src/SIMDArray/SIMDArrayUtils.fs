﻿module SIMDArrayUtils

/// <summary>
/// Utility function for use with SIMD higher order functions
/// When you don't have leftover elements
/// example:
/// Array.SIMD.Map (fun x -> x*x) nop array
/// Where array is divisible by your SIMD width or you don't
/// care about what happens to the leftover elements
/// </summary>
let inline nop _ = Unchecked.defaultof<_>

let inline checkNonNull arg =
    match box arg with
    | null -> nullArg "array"
    | _ -> ()


open System.Threading.Tasks
open System

let inline private applyTask fromInc toExc stride f =        
        let mutable i = fromInc
        while i < toExc do
            f i
            i <- i + stride

let inline private applyTaskAggregate fromInc toExc stride acc f : ^T =        
        let mutable i = fromInc
        let mutable acc = acc
        while i < toExc do
            acc <- f i acc
            i <- i + stride
        acc


let inline ForStride (fromInclusive : int) (toExclusive :int) (stride : int) (f : int -> unit) =
            
    let numStrides = (toExclusive-fromInclusive)/stride
    if numStrides > 0 then
        let numTasks = Math.Min(Environment.ProcessorCount,numStrides)
        let stridesPerTask = numStrides/numTasks
        let elementsPerTask = stridesPerTask * stride;
        let mutable remainderStrides = numStrides - (stridesPerTask*numTasks)
            
        let taskArray : Task[] = Array.zeroCreate numTasks
        let mutable index = 0    
        for i = 0 to taskArray.Length-1 do        
            let toExc =
                if remainderStrides = 0 then
                    index + elementsPerTask
                else
                    remainderStrides <- remainderStrides - 1
                    index + elementsPerTask + stride
            let fromInc = index;            
        
            taskArray.[i] <- Task.Factory.StartNew(fun () -> applyTask fromInc toExc stride f)                        
            index <- toExc
                        
        Task.WaitAll(taskArray)


let inline ForStrideAggregate (fromInclusive : int) (toExclusive :int) (stride : int) (acc: ^T) (f : int -> ^T -> ^T) combiner =      
    let numStrides = (toExclusive-fromInclusive)/stride
    if numStrides > 0 then
        let numTasks = Math.Min(Environment.ProcessorCount,numStrides)
        let stridesPerTask = numStrides/numTasks
        let elementsPerTask = stridesPerTask * stride;
        let mutable remainderStrides = numStrides - (stridesPerTask*numTasks)
          
        let taskArray : Task< ^T>[] = Array.zeroCreate numTasks
        let mutable index = 0    
        for i = 0 to taskArray.Length-1 do        
            let toExc =
                if remainderStrides = 0 then
                    index + elementsPerTask
                else
                    remainderStrides <- remainderStrides - 1
                    index + elementsPerTask + stride
            let fromInc = index;            
            taskArray.[i] <- Task< ^T>.Factory.StartNew(fun () -> applyTaskAggregate fromInc toExc stride acc f)                        
            index <- toExc
                        
        let mutable result = acc
        for i = 0 to taskArray.Length-1 do       
            result <- combiner result taskArray.[i].Result    
        result
    else
        acc

