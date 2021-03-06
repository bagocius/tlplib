﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.tinylabproductions.TLPLib.Data;
using com.tinylabproductions.TLPLib.Functional;
using UnityEngine;

namespace com.tinylabproductions.TLPLib.Concurrent {
  public static class Future {
    public static Future<A> a<A>(Act<Promise<A>> action)
      { return Future<A>.async(action); }

    public static Future<A> successful<A>(A value)
      { return Future<A>.successful(value); }

    public static Future<A> unfulfilled<A>()
      { return Future<A>.unfulfilled; }

    public static Future<A> delay<A>(float seconds, Fn<A> createValue) {
      return a<A>(p => ASync.WithDelay(seconds, () => p.complete(createValue())));
    }

    public static Future<A> delay<A>(float seconds, A value) {
      return a<A>(p => ASync.WithDelay(seconds, () => p.complete(value)));
    }

    public static Future<A> delayFrames<A>(int framesToSkip, Fn<A> createValue) {
      return a<A>(p => ASync.AfterXFrames(framesToSkip, () => p.complete(createValue())));
    }

    public static Future<A> delayFrames<A>(int framesToSkip, A value) {
      return a<A>(p => ASync.AfterXFrames(framesToSkip, () => p.complete(value)));
    }

    /**
     * Converts enumerable of futures into future of enumerable that is completed
     * when all futures complete.
     **/
    public static Future<A[]> sequence<A>(
      this IEnumerable<Future<A>> enumerable, string name=null
    ) {
      var completed = 0u;
      var sourceFutures = enumerable.ToList();
      var results = new A[sourceFutures.Count];
      return a<A[]>(p => {
        for (var idx = 0; idx < sourceFutures.Count; idx++) {
          var f = sourceFutures[idx];
          var fixedIdx = idx;
          f.onComplete(value => {
            results[fixedIdx] = value;
            completed++;
            if (completed == results.Length) p.tryComplete(results);
          });
        }
      });
    }

    /**
     * Returns result from the first future that completes.
     **/
    public static Future<A> firstOf<A>(this IEnumerable<Future<A>> enumerable) {
      return Future<A>.async(p => {
        foreach (var f in enumerable) f.onComplete(v => p.tryComplete(v));
      });
    }

    /**
     * Returns result from the first future that satisfies the predicate as a Some.
     * If all futures do not satisfy the predicate returns None.
     **/
    public static Future<Option<B>> firstOfWhere<A, B>
    (this IEnumerable<Future<A>> enumerable, Fn<A, Option<B>> predicate) {
      var futures = enumerable.ToList();
      return Future<Option<B>>.async(p => {
        var completed = 0;
        foreach (var f in futures)
          f.onComplete(a => {
            completed++;
            var res = predicate(a);
            if (res.isDefined) p.tryComplete(res);
            else if (completed == futures.Count) p.tryComplete(F.none<B>());
          });
      });
    }

    public static Future<Option<B>> firstOfSuccessful<A, B>
    (this IEnumerable<Future<Either<A, B>>> enumerable)
    { return enumerable.firstOfWhere(e => e.rightValue); }

    public static Future<Either<A[], B>> firstOfSuccessfulCollect<A, B>
    (this IEnumerable<Future<Either<A, B>>> enumerable) {
      return enumerable.firstOfSuccessfulCollect(_ => _.ToArray());
    }

    public static Future<Either<Collection, B>> firstOfSuccessfulCollect<A, B, Collection>(
      this IEnumerable<Future<Either<A, B>>> enumerable,
      Fn<IEnumerable<A>, Collection> collector
    ) {
      var futures = enumerable.ToArray();
      return futures.firstOfSuccessful().map(opt => opt.fold(
        /* If this future is completed, then all futures are completed with lefts. */
        () => Either<Collection, B>.Left(collector(futures.Select(f => f.value.get.leftValue.get))),
        Either<Collection, B>.Right
      ));
    }

    public static Future<Unit> fromCoroutine(IEnumerator enumerator) {
      return Future<Unit>.async(p => ASync.StartCoroutine(coroutineEnum(p, enumerator)));
    }

    public static Future<A> fromBusyLoop<A>(
      Fn<Option<A>> checker, YieldInstruction delay=null
    ) { return Future<A>.async(p => ASync.StartCoroutine(busyLoopEnum(delay, p, checker))); }

    /* Waits at most `timeoutSeconds` for the future to complete. Completes with
       exception produced by `onTimeout` on timeout. */
    public static Future<Either<B, A>> timeout<A, B>(
      this Future<A> future, float timeoutSeconds, Fn<B> onTimeout
    ) {
      // TODO: test me - how? Unity test runner doesn't support delays.
      var timeoutF = delay(timeoutSeconds, () => future.value.fold(
        // onTimeout() might have side effects, so we only need to execute it if
        // there is no value in the original future once the timeout hits.
        () => onTimeout().left().r<A>(),
        v => v.right().l<B>()
      ));
      return new[] { future.map(v => v.right().l<B>()), timeoutF }.firstOf();
    }

    /* Waits at most `timeoutSeconds` for the future to complete. Completes with
       TimeoutException<A> on timeout. */
    public static Future<Either<Duration, A>> timeout<A>(
      this Future<A> future, float timeoutSeconds
    ) => future.timeout(timeoutSeconds, () => Duration.fromSeconds(timeoutSeconds));

    /** Measures how much time has passed from call to timed to future completion. **/
    public static Future<Tpl<A, Duration>> timed<A>(this Future<A> future) {
      var startTime = Time.realtimeSinceStartup;
      return future.map(a => {
        var time = Time.realtimeSinceStartup - startTime;
        return F.t(a, Duration.fromSeconds(time));
      });
    }

    static IEnumerator busyLoopEnum<A>(YieldInstruction delay, Promise<A> p, Fn<Option<A>> checker) {
      var valOpt = checker();
      while (valOpt.isEmpty) {
        yield return delay;
        valOpt = checker();
      }
      p.complete(valOpt.get);
    }

    static IEnumerator coroutineEnum(Promise<Unit> p, IEnumerator enumerator) {
      yield return ASync.StartCoroutine(enumerator);
      p.complete(Unit.instance);
    }
  }
}
