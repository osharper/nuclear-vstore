#!/bin/bash
#set -ex

function time-limit {
  RED='\033[0;31m'
  GREEN='\033[0;32m'
  NC='\033[0m'

  killTimeout=${KILL_TIMEOUT:-"120"} #sec

  maxTimeExecution=$1; shift
  cmd="$@"

  TIMEOUT_EXIT_CODE=124

  begin=$(date +"%s")
  timeout -k $killTimeout $maxTimeExecution $cmd
  CMD_EXIT_CODE=`echo $?`

  if [ $CMD_EXIT_CODE -eq $TIMEOUT_EXIT_CODE ] ; then
    printf "${RED}ERROR:${NC} command \"$cmd\" was stoped by timeout ($(($maxTimeExecution / 60)) minutes $(($maxTimeExecution % 60)) seconds) \n"
    return $CMD_EXIT_CODE
  fi

  if [ $CMD_EXIT_CODE -ne 0 ] ; then
    printf "${RED}ERROR:${NC} command \"$cmd\" return exit code: $CMD_EXIT_CODE\n"
    return $CMD_EXIT_CODE
  fi

  termin=$(date +"%s")
  currTimeExecution=$(($termin-$begin))

  printf "${GREEN}INFO:${NC} $(($currTimeExecution / 60)) minutes and $(($currTimeExecution % 60)) seconds elapsed for \"$cmd\"\n"
  printf "${GREEN}INFO:${NC} it's ok, max time execution ($(($maxTimeExecution / 60)) minutes $(($maxTimeExecution % 60)) seconds) not exeeded.\n"

  return $CMD_EXIT_CODE
}


# Retries a command on failure.
# $1 - the max number of attempts
# $2... - the command to run
# example usage:
#   retry 5 ls -ltr foo
function retry {
  max_attempts="$1"; shift
  cmd="$@"
  attempt_num=1

  until $cmd
  do
    if (( attempt_num == max_attempts ))
    then
      echo "Attempt $attempt_num failed and there are no more attempts left!"
      return 1
    else
      echo "Attempt $attempt_num failed! Trying again in $attempt_num seconds..."
      sleep $(( attempt_num++ ))
    fi
  done
}
