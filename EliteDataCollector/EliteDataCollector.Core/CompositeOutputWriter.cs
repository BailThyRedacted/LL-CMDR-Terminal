using System;
using System.Collections.Generic;
using EliteDataCollector.Core.Services;

namespace EliteDataCollector.Core
{
    /// <summary>
    /// Composite logger that forwards output to multiple OutputWriter implementations.
    ///
    /// DESIGN PATTERN - Composite Pattern:
    /// ===================================
    /// The Composite pattern lets you combine multiple objects into a tree structure
    /// to represent part-whole hierarchies. Clients can treat individual objects
    /// and compositions of objects uniformly.
    ///
    /// In this case:
    /// - Individual objects: ConsoleOutputWriter, FileOutputWriter
    /// - Composite object: CompositeOutputWriter (combines many writers)
    /// - Client: MainCore (doesn't know or care which writer is used)
    ///
    /// BENEFITS:
    /// - Log to console AND file simultaneously
    /// - Easy to add new output types (event log, database, etc.)
    /// - Change combination at runtime without changing MainCore
    /// - Follows Open/Closed Principle (open for extension, closed for modification)
    ///
    /// EXAMPLE USAGE:
    /// var console = new ConsoleOutputWriter();
    /// var file = new FileOutputWriter();
    /// var both = new CompositeOutputWriter(console, file);
    ///
    /// both.WriteLine("Hello!");  // Goes to console AND file
    /// both.WriteLine(LogLevel.Error, "Crash!");  // Both see it
    ///
    /// TEACHING VALUE:
    /// This demonstrates how interfaces enable flexibility.
    /// We can mix-and-match different implementations without code changes.
    /// </summary>
    public class CompositeOutputWriter : OutputWriter
    {
        /// <summary>
        /// List of all writers we forward to.
        ///
        /// TEACHING NOTE - List<T>:
        /// A generic list that holds any type (in this case OutputWriter).
        /// You can add, remove, iterate, etc.
        /// The <T> is a "type parameter" - it makes lists type-safe.
        ///
        /// Without generics, you'd use ArrayList and cast everything.
        /// With generics, type safety is automatic.
        /// </summary>
        private readonly List<OutputWriter> _writers;

        /// <summary>
        /// Constructor: takes a variable number of OutputWriters.
        ///
        /// TEACHING NOTE - params Keyword:
        /// The "params" keyword allows passing any number of arguments.
        /// Example:
        ///   new CompositeOutputWriter(console)
        ///   new CompositeOutputWriter(console, file)
        ///   new CompositeOutputWriter(console, file, network, database)
        ///
        /// Inside the method, writers is an array: OutputWriter[]
        /// We convert it to a List for easier management.
        /// </summary>
        public CompositeOutputWriter(params OutputWriter[] writers)
        {
            // TEACHING NOTE - Null Check:
            // Fail fast if someone passes null.
            // It's better to crash during setup than silently fail later.
            _writers = new List<OutputWriter>(writers ?? throw new ArgumentNullException(nameof(writers)));

            // TEACHING NOTE - Guard Clause:
            // If no writers provided, that's probably a mistake.
            if (_writers.Count == 0)
                throw new ArgumentException("At least one OutputWriter must be provided", nameof(writers));
        }

        /// <summary>
        /// Write a line to all registered writers.
        /// </summary>
        public void WriteLine(string message)
        {
            // TEACHING NOTE - Foreach Loop over Collection:
            // Iterates through each writer in the list.
            // For each writer, call its WriteLine method.
            foreach (var writer in _writers)
            {
                // TEACHING NOTE - Null-Safe Invocation:
                // The ?. operator means "only call if not null".
                // If writer is null, this silently does nothing.
                // This is defensive but shouldn't happen with our setup.
                writer?.WriteLine(message);
            }
        }

        /// <summary>
        /// Write a formatted line to all registered writers.
        /// </summary>
        public void WriteLine(string format, params object[] args)
        {
            var message = string.Format(format, args);
            WriteLine(message);
        }

        /// <summary>
        /// Write a leveled log line to all registered writers.
        ///
        /// TEACHING NOTE - Method Overloading (again):
        /// We now have three WriteLine() methods:
        /// 1. WriteLine(string)
        /// 2. WriteLine(string, params object[])
        /// 3. WriteLine(LogLevel, string)
        ///
        /// C# automatically picks the right one based on your parameters.
        /// This is powerful but can be confusing - keep overloads simple!
        /// </summary>
        public void WriteLine(LogLevel level, string message)
        {
            foreach (var writer in _writers)
            {
                // TEACHING NOTE - Casting/Type Checking:
                // Some writers (like ConsoleOutputWriter) support LogLevel.
                // Others might not. We check the actual type and call accordingly.

                // Try to cast the writer to an interface that supports LogLevel
                // (In this case, ConsoleOutputWriter has the method directly)
                if (writer is ConsoleOutputWriter consoleWriter)
                {
                    consoleWriter.WriteLine(level, message);
                }
                else if (writer is FileOutputWriter fileWriter)
                {
                    fileWriter.WriteLine(level, message);
                }
                else
                {
                    // Fall back to basic message (no level)
                    writer.WriteLine(message);
                }
            }
        }

        /// <summary>
        /// Write a formatted leveled log line to all registered writers.
        /// </summary>
        public void WriteLine(LogLevel level, string format, params object[] args)
        {
            var message = string.Format(format, args);
            WriteLine(level, message);
        }

        /// <summary>
        /// Add a new writer to the composite.
        /// Useful if you want to add logging destinations at runtime.
        ///
        /// TEACHING NOTE - Runtime Modification:
        /// This lets you add writers dynamically without restarting.
        /// Example: Add remote logging only if configured
        /// Example: Add verbose debugging when needed
        /// </summary>
        public void AddWriter(OutputWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            _writers.Add(writer);
        }

        /// <summary>
        /// Remove a writer from the composite.
        /// Returns true if found and removed, false if not found.
        /// </summary>
        public bool RemoveWriter(OutputWriter writer)
        {
            return _writers.Remove(writer);
        }

        /// <summary>
        /// Get the number of registered writers.
        /// Useful for testing/debugging.
        /// </summary>
        public int WriterCount => _writers.Count;
    }
}