# fastgithub 命令行自动补全（bash/zsh）
# 安装：echo "source /path/to/FastGithub-Project/completions/fastgithub-completion.bash" >> ~/.bashrc && source ~/.bashrc
# 卸载：complete -r fastgithub

_fastgithub_completion()
{
    local cur prev
    COMPREPLY=()
    cur="${COMP_WORDS[COMP_CWORD]}"
    prev="${COMP_WORDS[COMP_CWORD-1]}"

    local subcommands="start stop config"
    local options="-h --help --port --listen --dns-listen --dns-port --dns --log-level --udp-port"
    local config_actions="list get set unset"

    # 第一层：指令与参数
    if [ "$COMP_CWORD" -eq 1 ]; then
        COMPREPLY=( $(compgen -W "${subcommands} ${options}" -- "${cur}") )
        return 0
    fi

    # config 子命令
    if [ "${COMP_WORDS[1]}" = "config" ]; then
        # 第二层：动作
        if [ "$COMP_CWORD" -eq 2 ]; then
            COMPREPLY=( $(compgen -W "${config_actions}" -- "${cur}") )
            return 0
        fi

        # 第三层：仅键位（第3个参数）补全配置键，运行动态读取；
        # config get/unset 只收一个键，config set 的第4位是值，均不再补全键
        if [ "$COMP_CWORD" -eq 3 ]; then
            case "${COMP_WORDS[2]}" in
                get|set|unset)
                    local keys
                    keys=$(fastgithub config list 2>/dev/null | cut -d= -f1)
                    COMPREPLY=( $(compgen -W "${keys}" -- "${cur}") )
                    return 0
                    ;;
            esac
        fi
    fi

    # 参数的值：给出常用取值
    case "$prev" in
        --port|--udp-port)
            COMPREPLY=( $(compgen -W "38457 38458 8080 9000" -- "${cur}") )
            ;;
        --dns-port)
            COMPREPLY=( $(compgen -W "53 5353 8053" -- "${cur}") )
            ;;
        --listen|--dns-listen)
            COMPREPLY=( $(compgen -W "localhost 0.0.0.0" -- "${cur}") )
            ;;
        --log-level)
            COMPREPLY=( $(compgen -W "Verbose Debug Information Warning Error Fatal" -- "${cur}") )
            ;;
        --dns)
            COMPREPLY=( $(compgen -W "8.8.8.8:53 119.29.29.29:53 114.114.114.114:53" -- "${cur}") )
            ;;
    esac

    return 0
}

complete -F _fastgithub_completion fastgithub
complete -F _fastgithub_completion fastgithub.exe
