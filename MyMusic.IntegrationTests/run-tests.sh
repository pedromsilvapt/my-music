#!/bin/sh
# Runs the integration tests and, on failure, prints the names of the failed tests at the end.
dotnet vstest --Settings:/app/bin/integration.runsettings MyMusic.IntegrationTests.dll \
    --logger:"console;verbosity=minimal" \
    --logger:"trx;LogFileName=results.trx" --ResultsDirectory:/app/results "$@"
code=$?

trx=/app/results/results.trx
if [ "$code" -ne 0 ] && [ -f "$trx" ]; then
    echo
    echo "========== Failed tests =========="
    grep -o '<UnitTestResult [^>]*outcome="Failed"[^>]*>' "$trx" \
        | sed -e 's/.*testName="\([^"]*\)".*/  - \1/' \
              -e 's/&quot;/"/g; s/&apos;/'"'"'/g; s/&lt;/</g; s/&gt;/>/g; s/&amp;/\&/g'
    echo "=================================="
fi

exit "$code"
