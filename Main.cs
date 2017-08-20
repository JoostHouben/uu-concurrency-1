using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Security.Cryptography;

namespace b3cc_opgave1
{
    // abstract class to facilitate locking
    abstract class Lock
    {
        // Returns an object which, when disposed, releases the lock
        // This return value can be ignored, but it is convenient to use it in a using-block
        // (especially considering exception-safety and such)
        public abstract IDisposable EnterCritical();

        public abstract void ExitCritical();

        // The return value of EnterCritical
        // There's no need to expose it to other parts of the program, so it is only visible as an 'IDisposable'
        protected class AttainedLock : IDisposable
        {
            private Lock myLock;

            public AttainedLock(Lock myLock)
            {
                this.myLock = myLock;
            }

            public void Dispose()
            {
                myLock.ExitCritical();
            }
        }
    }

    // The lock I implemented: the Test-and-Set-lock.
    class TaSLock : Lock
    {
        private int locked;

        public override IDisposable EnterCritical()
        {
            while (Interlocked.Exchange(ref locked, 1) == 1) ;
            return new AttainedLock(this);
        }

        public override void ExitCritical()
        {
            locked = 0;
        }
    }

    // The lock which uses the built-in locking mechanisms
    // We don't actually use the lock-keyword, because it should support the same interface as the TaSLock.
    class StandardLock : Lock
    {
        // the object on which we will lock
        private object lockObject = new object();

        public override IDisposable EnterCritical()
        {
            Monitor.Enter(lockObject); // same as entering a lock(lockObject)-block
            return new AttainedLock(this);
        }

        public override void ExitCritical()
        {
            Monitor.Exit(lockObject); // same as leaving a lock(lockObject)-block
        }
    }

    // A class I routinely use to make C#-I/O more C++-like
    // Essentially it does tokenizing
    // It splits the input on spaces (and newlines), and allows the user to directly read common datatypes
    // Also allows checking if the input stream has ended
    // (Since I have used this class before, submit might complain about plagiarism; I have written this code myself
    // for use in UU programming assignments; I have not shared it with anyone.)
    static class ConsoleWrapper
    {
        private static string[] tokens;
        private static int index;

        public static bool IsEmpty
        {
            get
            {
                string v = Read();
                index--;
                return v == null;
            }
        }

        public static string Read()
        {
            if (index < 0)
                return null;
            if (tokens == null || index == tokens.Length)
            {
                string input = Console.ReadLine();
                if (input == null)
                {
                    index = -1;
                    return null;
                }
                tokens = input.Split(' ');
                index = 0;
            }
            return tokens[index++];
        }

        public static int ReadInt()
        {
            return int.Parse(Read());
        }

        public static long ReadLong()
        {
            return long.Parse(Read());
        }

        public static double ReadDouble()
        {
            return double.Parse(Read());
        }
    }

    // Main class
    class IbanCalc
    {
        static void Main(string[] args)
        {
            int l = ConsoleWrapper.ReadInt(); // lock type
            int b = ConsoleWrapper.ReadInt(); // lower bound
            int e = ConsoleWrapper.ReadInt(); // upper bound
            int m = ConsoleWrapper.ReadInt(); // modulus
            int p = ConsoleWrapper.ReadInt(); // threads
            int u = ConsoleWrapper.ReadInt(); // mode: 0 = count; 1 = list; 2 = search
            string h = "";
            if(u == 2)
                h = ConsoleWrapper.Read(); // hash to search for

            Lock myLock;
            switch(l) {
                case 0:
                    myLock = new TaSLock();
                    break;

                case 1:
                    myLock = new StandardLock();
                    break;

                default:
                    Console.WriteLine("Invalid value for 'l'.");
                    return;
            }
            
            SharedResource resource;
            switch(u) {
                case 0:
                case 1:
                    resource = new CountResource();
                    break;

                case 2:
                    resource = new SearchResource(h);
                    break;

                default:
                    Console.WriteLine("Invalid value for 'u'");
                    return;
            }

            List<Worker> workers = new List<Worker>();
            for(int i=0; i<p; i++) {
                int start = b + (int)((long)(e - b) * i / p);
                int end = b + (int)((long)(e - b) * (i + 1) / p);

                switch(u) {
                    case 0:
                        workers.Add(new CountWorker(myLock, (CountResource)resource, start, end, m));
                        break;

                    case 1:
                        workers.Add(new ListWorker(myLock, (CountResource)resource, start, end, m));
                        break;

                    case 2:
                        workers.Add(new SearchWorker(myLock, (SearchResource)resource, start, end, m));
                        break;
                }

                workers.Last().Start();
            }

            foreach(Worker w in workers) {
                w.Wait();
            }

            switch (u)
            {
                case 0:
                    Console.WriteLine(((CountResource)resource).Count);
                    break;

                case 2:
                    Console.Write(((SearchResource)resource).Found);
                    break;
            }
        }
    }

    // The resource which the Worker's will share
    // SharedResource is thread-unaware (any mutations on it should be synchronized externally)
    // SharedResource also owns a flag to signal that threads should terminate
    abstract class SharedResource
    {
        public abstract bool ShouldStop { get; }
    }

    // The abstract worker class
    // Has three implementers, one for each mode
    // Forms the interface around the thread
    // The abstract class itself takes care of looping, modulo-M-test, and starting and stopping the thread
    abstract class Worker
    {
        private readonly Lock myLock;

        public readonly SharedResource SharedResource;
        public readonly int LowerBound;
        public readonly int UpperBound;
        public readonly int Modulus;

        private Thread thread = null;

        public Worker(Lock myLock, SharedResource sharedResource, int lowerBound, int upperBound, int modulus)
        {
            this.myLock = myLock;
            SharedResource = sharedResource;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            Modulus = modulus;
        }

        // start the work
        public void Start()
        {
            if (thread != null)
                throw new InvalidOperationException("Thread already started");

            thread = new Thread(work);
            thread.Start();
        }

        // wait for the work to finish (using Join)
        public void Wait()
        {
            if (thread == null)
                return;
            thread.Join();
        }

        // main loop for the thread
        private void work()
        {
            // reading the ShouldStop flag doesn't have to be synchronized
            // (Since the exact moment of termination is irrelevant; it only matters that the signal is
            // propagated to the other threads as quickly as possible)
            for (int i = LowerBound; i < UpperBound && !SharedResource.ShouldStop; i++)
            {
                if (moduloMCheck(i))
                {
                    validWork(i);
                }
            }
        }

        // code to execute for numbers that pas the moduloMCheck
        // in search mode, this method is overrriden.
        protected virtual void validWork(int number)
        {
            using (myLock.EnterCritical())
            {
                criticalWork(number);
            }
        }

        // mode-dependent critical work
        protected abstract void criticalWork(int number);

        public bool moduloMCheck(int number)
        {
            int total = 0;
            for (int i = 1; number != 0; i++)
            {
                total += i * (number % 10);
                number /= 10;
            }
            return total % Modulus == 0;
        }
    }

    // A shared resource which represents a counter, and will never tell the threads to terminate.
    class CountResource : SharedResource
    {
        public int Count { get; set; }

        public override bool ShouldStop
        {
            get { return false; }
        }

        public CountResource()
        {
            Count = 0;
        }
    }

    // Counting mode: critical work consists of incrementing the shared counter
    class CountWorker : Worker
    {
        public CountWorker(Lock myLock, CountResource sharedResource, int lowerBound, int upperBound, int modulus)
            : base(myLock, sharedResource, lowerBound, upperBound, modulus) { }

        protected override void criticalWork(int number)
        {
            ((CountResource)SharedResource).Count++;
        }
    }

    // List mode: critical work consists of incrementing the shared counter, and doing I/O
    class ListWorker : Worker
    {
        public ListWorker(Lock myLock, CountResource sharedResource, int lowerBound, int upperBound, int modulus)
            : base(myLock, sharedResource, lowerBound, upperBound, modulus) { }

        protected override void criticalWork(int number)
        {
            Console.WriteLine("{0} {1}", ++((CountResource)SharedResource).Count, number);
        }
    }

    // A shared resource that owns a hash to search for, 
    // and an integer that indicates where the hash was found, or -1 if it wasn't
    class SearchResource : SharedResource
    {
        public int Found { get; set; }
        public readonly string HashToFind;
        
        // stop if we found the hash
        public override bool ShouldStop
        {
            get { return Found != -1; }
        }

        public SearchResource(string hashToFind)
        {
            Found = -1;
            HashToFind = hashToFind;
        }
    }

    class SearchWorker : Worker
    {
        private readonly SHA1 sha = SHA1.Create();

        public SearchWorker(Lock myLock, SearchResource sharedResource, int lowerBound, int upperBound, int modulus)
            : base(myLock, sharedResource, lowerBound, upperBound, modulus) { }

        // compute and compare the hash without locking
        protected override void validWork(int number)
        {
            string toHash = string.Format("{0:D9}", number); // is length 9 correct? Doesn't say in assignment..
            byte[] hashArray = sha.ComputeHash(Encoding.ASCII.GetBytes(toHash));
            string hash = "";
            foreach (byte b in hashArray)
                hash += b.ToString("x2");
            // since HashToFind is only read from, no synchronization is neccesary
            if (hash != ((SearchResource)SharedResource).HashToFind)
                return;

            // now we can enter the critical section (using the base's locking logic)
            base.validWork(number);
        }

        // if we get here, the hash is correct, no need to check...
        protected override void criticalWork(int number)
        {
            ((SearchResource)SharedResource).Found = number; // ...so simply set found to this number. This will also set ShouldStop to true.
            // Note that the guarantee that there will be at most one number to match the hash means synchronization isn't strictly
            // neccessary for this section. However for the same reason, there's also very little overhead involved in entering this
            // critical section. So it seems like a good idea to use synchronization for this section anyway, for the unlikely case
            // that a hash collision does happen.
        }
    }
}
