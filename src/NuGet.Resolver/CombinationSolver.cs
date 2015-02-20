using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NuGet.Resolver
{
    /// <summary>
    /// This class is responsible for finding the best combination of compatible items. The caller
    /// supplies a collection of groups, a sorting function (to determine priority within a group), and
    /// a function to determine whether two items are incompatible. The solution (if found) will contain 
    /// exactly 1 item from each group.
    /// </summary>
    /// <remarks>Created by Aaron Marten</remarks>
    /// <typeparam name="T">The type of item to evaluate.</typeparam>
    public class CombinationSolver<T>
    {
        private T[] solution;

        /// <summary>
        /// The initial domains are the full/initial candidate sets we start with when 
        /// attempting to discover a solution. They need to be stored and referred to
        /// as the algorithm executes to re-initialize the current/working domains.
        /// </summary>
        private IList<IEnumerable<T>> initialDomains;

        /// <summary>
        /// The current domains are initialized with the initial domains. As we progress
        /// through the algorithm, we may remove elements from the current domain as we
        /// discover that an item cannot be part of the solution. If we need to backtrack,
        /// we may reset the current domain to the corresponding initial domain.
        /// </summary>
        private List<HashSet<T>> currentDomains;

        /// <summary>
        /// The subset of past indexes where a conflict was found. Used to calculate the biggest and safest
        /// (i.e. not missing a better solution) jump we can make in MoveBackward.
        /// </summary>
        private List<HashSet<int>> conflictSet;

        /// <summary>
        /// For each position, maintain a stack of past indexes that forward checked (and found/removed conflicts)
        /// from the position.
        /// </summary>
        private List<Stack<int>> pastForwardChecking;

        /// <summary>
        /// For each position, maintain a stack of forward/future indexes where conflicts were found.
        /// </summary>
        private List<Stack<int>> futureForwardChecking;

        /// <summary>
        /// For each position, maintain a Stack of sets of items that were 'reduced' from the domain. This allows us
        /// to restore the items back into the domain on future iterations in case we need to back up, etc...
        /// </summary>
        private List<Stack<Stack<T>>> reductions;
        private IComparer<T> prioritySorter;
        private Func<T, T, bool> shouldRejectPair;

        /// <summary>
        /// Entry point for the combination evalutation phase of the algorithm. The algorithm
        /// combines forward checking [FC] (i.e. trying to eliminate future possible combinations to evaluate)
        /// with Conflict-directed Back Jumping.
        /// 
        /// Based off the FC-CBJ algorithm described in Prosser's Hybrid
        /// Algorithms for the Constraint Satisfaction Problem: http://archive.nyu.edu/bitstream/2451/14410/1/IS-90-10.pdf
        /// </summary>
        /// <param name="groupedItems">The candidate enlistment items grouped by product.</param>
        /// <param name="itemSorter">Function supplied by the caller to sort items in preferred/priority order. 'Higher priority' items should come *first* in the sort.</param>
        /// <param name="shouldRejectPairFunc">Function supplied by the caller to determine whether two items are compatible or not.</param>
        /// <returns>The 'best' solution (if one exists). Null otherwise.</returns>
        public IEnumerable<T> FindSolution(IEnumerable<IEnumerable<T>> groupedItems,
                                                       IComparer<T> itemSorter,
                                                       Func<T, T, bool> shouldRejectPairFunc)
        {
            bool consistent = true;
            int i = 0;
            this.prioritySorter = itemSorter;
            this.shouldRejectPair = shouldRejectPairFunc;

            initialDomains = groupedItems.ToList();

            //Initialize various arrays required for the algorithm to run.
            currentDomains = initialDomains.Select(d => new HashSet<T>(d)).ToList();
            conflictSet = initialDomains.Select(d => new HashSet<int>()).ToList();
            pastForwardChecking = initialDomains.Select(d => new Stack<int>()).ToList();
            futureForwardChecking = initialDomains.Select(d => new Stack<int>()).ToList();
            reductions = initialDomains.Select(d => new Stack<Stack<T>>()).ToList();

            solution = new T[initialDomains.Count];

            while (true)
            {
                i = consistent ? MoveForward(i, ref consistent) : MoveBackward(i, ref consistent);

                if (i > solution.Length)
                {
                    throw new Exception("Evaluated past the end of the array.");
                }
                else if (i == solution.Length)
                {
                    return solution;
                }
                else if (i < 0)
                {
                    //Impossible (no solution)
                    return null;
                }
            }
        }

        /// <summary>
        /// Attempts to populate the element at position i with a consistent possibility 
        /// and move forward to the next element in the sequence.
        /// </summary>
        /// <param name="i">The position in the solution to attempt to populate.</param>
        /// <param name="consistent">Upon completion, set to true if the function was able to find a candidate to populate position i with. False otherwise.</param>
        /// <returns>The next position to evaluate if consistent is true. If false, return value is the value to move back to.</returns>
        private int MoveForward(int i, ref bool consistent)
        {
            consistent = false;

            //Call ToList so we can potentially remove the currentItem from currentDomains[i] as we're iterating
            foreach (var currentItem in currentDomains[i].OrderBy(x => x, prioritySorter).ToList())
            {
                if (consistent)
                {
                    break;
                }

                consistent = true;
                solution[i] = currentItem;

                for (int j = i + 1; j < currentDomains.Count && consistent; j++)
                {
                    consistent = CheckForward(i, j);
                    if (!consistent)
                    {
                        currentDomains[i].Remove(currentItem);
                        UndoReductions(i);
                        conflictSet[i].UnionWith(pastForwardChecking[j]);
                    }
                }
            }

            return consistent ? i + 1 : i;
        }

        /// <summary>
        /// Attempts to move back in the algorithm from position i.
        /// </summary>
        /// <param name="i">The position to unset / move back from.</param>
        /// <param name="consistent">True if backwards move was successful and algorithm can move forward again. False if the algorithm should continue to move backwards.</param>
        /// <returns>The position that the call was able to safely move back to.</returns>
        private int MoveBackward(int i, ref bool consistent)
        {
            if (i < 0 || i >= solution.Length)
            {
                throw new ArgumentException("MoveBackward called with invalid value for i.", "i");
            }

            if (i == 0 && !consistent)
            {
                //We're being asked to back up from the starting position. No solution is possible.
                return -1;
            }

            var max = new Func<IEnumerable<int>, int>(enumerable => (enumerable == null || !enumerable.Any()) ? 0 : enumerable.Max());

            //h is the index we can *safely* move back to
            int h = Math.Max(max(conflictSet[i]), max(pastForwardChecking[i]));
            conflictSet[h] = new HashSet<int>(conflictSet[i].Union(pastForwardChecking[i]).Union(conflictSet[h]).Except(new[] { h }));

            for (int j = i; j > h; j--)
            {
                conflictSet[j].Clear();
                UndoReductions(j);
                UpdateCurrentDomain(j);
            }

            UndoReductions(h);
            currentDomains[h].Remove(solution[h]);
            consistent = currentDomains[h] != null && currentDomains[h].Any();

            return h;
        }

        /// <summary>
        /// Performs forward checking between the already selected element at position i
        /// and potential candidates at position j.
        /// </summary>
        /// <param name="i">The position of the current element.</param>
        /// <param name="j">The position of the future domain to check against.</param>
        /// <returns>True if there are still remaining possibilities in the future domain. False if all possibilities have been eliminated.</returns>
        private bool CheckForward(int i, int j)
        {
            var reductionAgainstFutureDomain = new Stack<T>();
            foreach (var itemInFutureDomain in currentDomains[j].OrderBy(x => x, prioritySorter))
            {
                solution[j] = itemInFutureDomain;

                if (shouldRejectPair(solution[i], solution[j]))
                {
                    reductionAgainstFutureDomain.Push(itemInFutureDomain);
                }
            }

            if (reductionAgainstFutureDomain.Count > 0)
            {
                //Remove the items from the future domain
                currentDomains[j].ExceptWith(reductionAgainstFutureDomain);

                //Store the items we just removed as a 'reduction' against the future domain.
                reductions[j].Push(reductionAgainstFutureDomain);

                //Record that we've done future forward checking/reduction from i=>j
                futureForwardChecking[i].Push(j);

                //Likewise in the past array, store that we've done forward checking/reduction from j=>i
                pastForwardChecking[j].Push(i);
            }

            return currentDomains[j].Count > 0;
        }

        /// <summary>
        /// Undo reductions that were previously performed from position i.
        /// </summary>
        /// <param name="i">The position to undo reductions from.</param>
        private void UndoReductions(int i)
        {
            foreach (int j in futureForwardChecking[i])
            {
                var reduction = reductions[j].Pop();
                currentDomains[j].UnionWith(reduction);

                var pfc = pastForwardChecking[j].Pop();
                Debug.Assert(i == pfc);
            }

            futureForwardChecking[i].Clear();
        }

        /// <summary>
        /// Reinitialize the current domain to its initial value and apply any reductions against it.
        /// </summary>
        /// <param name="i">The position of the domain to update.</param>
        private void UpdateCurrentDomain(int i)
        {
            // Initialize it to the original domain values. Since currentDomain[i] will be
            // manipulated throughout the algorithm, it is critical to create a *new* set at this
            // point to avoid having initialDomains[i] be tampered with.
            currentDomains[i] = new HashSet<T>(initialDomains[i]);

            //Remove any current reduction items
            foreach (var reduction in reductions[i])
            {
                currentDomains[i].ExceptWith(reduction);
            }
        }
    }
}
